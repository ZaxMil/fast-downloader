package com.zaxmill.fastdownloader

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.ContentValues
import android.content.Context
import android.content.Intent
import android.os.Environment
import android.os.IBinder
import android.os.StatFs
import android.provider.MediaStore
import android.webkit.MimeTypeMap
import com.yausername.ffmpeg.FFmpeg
import com.yausername.youtubedl_android.YoutubeDL
import com.yausername.youtubedl_android.YoutubeDLRequest
import java.io.File
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Alpha media engine: one job at a time; yt-dlp owns partial-file continuation.
 * The foreground service keeps working when the Activity is closed.
 * A paused/failed job can be resumed using the same workspace.
 */
class MediaDownloadService : Service() {
    companion object {
        const val START = "com.zaxmill.fastdownloader.START"
        const val PAUSE = "com.zaxmill.fastdownloader.PAUSE"
        const val RESUME = "com.zaxmill.fastdownloader.RESUME"
        const val CANCEL = "com.zaxmill.fastdownloader.CANCEL"
        private const val CHANNEL = "video_downloads"
        private const val NOTIFY_ID = 402
        @Volatile var activeJobId: String? = null
            private set

        fun send(context: Context, action: String) {
            val intent = Intent(context, MediaDownloadService::class.java).setAction(action)
            if (action == START || action == RESUME) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }
    }

    private val stopping = AtomicBoolean(false)
    @Volatile private var worker: Thread? = null
    private val notifications by lazy { getSystemService(NOTIFICATION_SERVICE) as NotificationManager }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        notifications.createNotificationChannel(
            NotificationChannel(CHANNEL, "Video downloads", NotificationManager.IMPORTANCE_LOW)
        )
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            START, RESUME -> {
                // Android requires a prompt foreground notification before engine initialization.
                startForeground(NOTIFY_ID, notification("Preparing media engine", 0, true))
                val job = JobStore.read(this)
                if (job == null || !UrlTools.isHttps(job.url)) {
                    stopJob("Invalid URL")
                    return START_NOT_STICKY
                }
                if (worker?.isAlive == true) return START_NOT_STICKY
                stopping.set(false)
                activeJobId = job.id
                JobStore.save(this, job.copy(state = "Starting", detail = "Preparing downloader"))
                worker = Thread({ runJob(job) }, "FastDownloader-media").also { it.start() }
            }
            PAUSE, CANCEL -> {
                stopping.set(true)
                activeJobId?.let { YoutubeDL.destroyProcessById(it) }
                val job = JobStore.read(this)
                if (job != null) {
                    JobStore.save(this, job.copy(
                        state = if (intent.action == PAUSE) "Paused" else "Canceled",
                        detail = if (intent.action == PAUSE) "Resume when ready" else "Canceled by user"
                    ))
                    if (intent.action == CANCEL) {
                        // Keep partial files until the worker exits to prevent races.
                        worker?.interrupt()
                    }
                }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
            }
        }
        return START_NOT_STICKY
    }

    private fun runJob(job: MediaJob) {
        val taskDir = File(
            getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS) ?: filesDir,
            "media-tasks/${job.id}"
        )
        try {
            taskDir.mkdirs()
            require(taskDir.isDirectory) { "Cannot create download directory" }
            require(StatFs(taskDir.absolutePath).availableBytes > 50L * 1024L * 1024L) {
                "Not enough free storage"
            }
            YoutubeDL.getInstance().init(applicationContext)
            FFmpeg.getInstance().init(applicationContext)
            if (stopping.get()) return
            val request = YoutubeDLRequest(job.url)
            request.addOption("--no-playlist")
            request.addOption("--continue")
            request.addOption("--no-mtime")
            request.addOption("--newline")
            request.addOption("--paths", taskDir.absolutePath)
            request.addOption("-o", "%(title).100B [%(id)s].%(ext)s")
            when (job.quality) {
                "720" -> request.addOption("-f", "bv*[height<=720]+ba/b[height<=720]/b")
                "480" -> request.addOption("-f", "bv*[height<=480]+ba/b[height<=480]/b")
                "audio" -> {
                    request.addOption("-f", "bestaudio/best")
                    request.addOption("-x")
                    request.addOption("--audio-format", "mp3")
                }
                else -> request.addOption("-f", "bv*+ba/b")
            }
            if (job.quality != "audio") request.addOption("--merge-output-format", "mp4")
            var lastUpdate = 0L
            YoutubeDL.getInstance().execute(request, job.id) { progress, eta, _ ->
                if (!stopping.get()) {
                    val now = System.currentTimeMillis()
                    if (now - lastUpdate > 1100L || progress >= 99.9f) {
                        lastUpdate = now
                        val percent = progress.toInt().coerceIn(0, 99)
                        JobStore.save(this, job.copy(
                            state = "Downloading", progress = percent,
                            detail = if (eta > 0) "About ${eta}s remaining" else "Downloading"
                        ))
                        notifications.notify(NOTIFY_ID, notification("Downloading media", percent, false))
                    }
                }
            }
            if (stopping.get()) return
            val completed = taskDir.listFiles()?.filter {
                it.isFile && it.length() > 0 &&
                    !it.name.endsWith(".part") &&
                    !it.name.endsWith(".ytdl") &&
                    !it.name.endsWith(".temp")
            }.orEmpty()
            require(completed.isNotEmpty()) { "No completed media file was produced" }
            var published = 0
            for (file in completed) {
                publishToDownloads(file)
                published++
            }
            taskDir.deleteRecursively()
            JobStore.save(this, job.copy(state = "Completed", progress = 100,
                detail = "Saved ${published} file(s) in Downloads/FastDownloader"))
            notifications.notify(NOTIFY_ID, notification("Download complete", 100, false))
        } catch (e: Exception) {
            if (!stopping.get()) {
                val reason = e.message?.take(170) ?: "Could not download media"
                JobStore.save(this, job.copy(state = "Failed", detail = reason))
                notifications.notify(NOTIFY_ID, notification("Download failed", 0, false))
            }
        } finally {
            activeJobId = null
            worker = null
            stopForeground(STOP_FOREGROUND_REMOVE)
            stopSelf()
        }
    }

    private fun publishToDownloads(file: File) {
        val mime = MimeTypeMap.getSingleton().getMimeTypeFromExtension(
            file.extension.lowercase()
        ) ?: "application/octet-stream"
        val values = ContentValues().apply {
            put(MediaStore.MediaColumns.DISPLAY_NAME, file.name)
            put(MediaStore.MediaColumns.MIME_TYPE, mime)
            put(MediaStore.MediaColumns.RELATIVE_PATH,
                "${Environment.DIRECTORY_DOWNLOADS}/FastDownloader")
            put(MediaStore.MediaColumns.IS_PENDING, 1)
        }
        val uri = contentResolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
            ?: error("Android Downloads storage is unavailable")
        try {
            contentResolver.openOutputStream(uri)?.use { output ->
                file.inputStream().use { input -> input.copyTo(output) }
            } ?: error("Could not write downloaded file")
            contentResolver.update(uri, ContentValues().apply {
                put(MediaStore.MediaColumns.IS_PENDING, 0)
            }, null, null)
        } catch (e: Exception) {
            contentResolver.delete(uri, null, null)
            throw e
        }
    }

    private fun notification(title: String, percent: Int, indeterminate: Boolean): Notification {
        val intent = PendingIntent.getActivity(
            this, 0, Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        return Notification.Builder(this, CHANNEL)
            .setSmallIcon(android.R.drawable.stat_sys_download)
            .setContentTitle("FastDownloader")
            .setContentText(title)
            .setContentIntent(intent)
            .setOnlyAlertOnce(true)
            .setOngoing(indeterminate || percent in 1..99)
            .setProgress(100, percent, indeterminate)
            .build()
    }

    private fun stopJob(reason: String) {
        JobStore.read(this)?.let { JobStore.save(this, it.copy(state = "Failed", detail = reason)) }
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }
}
