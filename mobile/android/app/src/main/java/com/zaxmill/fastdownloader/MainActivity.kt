package com.zaxmill.fastdownloader

import android.Manifest
import android.app.DownloadManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.delay
import java.net.URI
import java.util.UUID

class MainActivity : ComponentActivity() {
    private val incomingLink = mutableStateOf("")

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        acceptLink(intent)
        if (Build.VERSION.SDK_INT >= 33 &&
            checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), 701)
        setContent {
            MaterialTheme(colorScheme = darkColorScheme(
                background = Color(0xFF0B1019),
                surface = Color(0xFF141D2B),
                primary = Color(0xFF53E6AD),
                secondary = Color(0xFF70ACED)
            )) { DownloadHome(incomingLink.value) }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        acceptLink(intent)
    }

    private fun acceptLink(intent: Intent?) {
        if (intent?.action != Intent.ACTION_SEND) return
        intent.getStringExtra(Intent.EXTRA_TEXT)?.let { shared ->
            UrlTools.fromSharedText(shared)?.let { incomingLink.value = it }
        }
    }
}

internal fun queueDirectDownload(context: Context, input: String): Result<Long> = runCatching {
    require(UrlTools.isHttps(input)) { "Enter a valid public HTTPS file URL." }
    val uri = URI(input.trim())
    val originalName = Uri.decode(uri.path?.substringAfterLast('/') ?: "")
    val filename = originalName
        .replace(Regex("""[\\/\u0000-\u001f\u007f:]"""), "_")
        .trim('.', ' ')
        .take(100)
        .ifBlank { "download" }
    val request = DownloadManager.Request(Uri.parse(uri.toASCIIString()))
        .setTitle(filename)
        .setDescription("Direct file — Android system download")
        .setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED)
        .setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS, filename)
        .setAllowedOverMetered(true)
        .setAllowedOverRoaming(false)
    (context.getSystemService(Context.DOWNLOAD_SERVICE) as DownloadManager)
        .enqueue(request).also { JobStore.saveDirect(context, it) }
}

private fun directStatus(context: Context, id: Long): String {
    if (id == -1L) return "No direct file queued yet"
    val manager = context.getSystemService(Context.DOWNLOAD_SERVICE) as DownloadManager
    return runCatching {
        manager.query(DownloadManager.Query().setFilterById(id))?.use { cursor ->
            if (!cursor.moveToFirst()) return@use "Download not found in Android Downloads"
            when (cursor.getInt(cursor.getColumnIndexOrThrow(DownloadManager.COLUMN_STATUS))) {
                DownloadManager.STATUS_SUCCESSFUL -> "Completed — see Android Downloads"
                DownloadManager.STATUS_RUNNING -> "Downloading in background"
                DownloadManager.STATUS_PAUSED -> "Waiting for Android download service / network"
                DownloadManager.STATUS_FAILED -> "Failed — check link or free storage"
                else -> "Queued by Android"
            }
        } ?: "Download status unavailable"
    }.getOrElse { "Could not read Android Downloads status" }
}

@Composable
private fun DownloadHome(sharedLink: String) {
    val context = LocalContext.current
    var url by rememberSaveable { mutableStateOf(sharedLink) }
    var mediaMode by rememberSaveable { mutableStateOf(true) }
    var quality by rememberSaveable { mutableStateOf("best") }
    var qualityMenu by remember { mutableStateOf(false) }
    var message by remember { mutableStateOf("") }
    var mediaJob by remember { mutableStateOf(JobStore.read(context)) }
    var directInfo by remember { mutableStateOf(directStatus(context, JobStore.directId(context))) }

    LaunchedEffect(sharedLink) { if (sharedLink.isNotBlank()) url = sharedLink }
    LaunchedEffect(Unit) {
        while (true) {
            delay(1250)
            mediaJob = JobStore.read(context)
            directInfo = directStatus(context, JobStore.directId(context))
        }
    }
    val busy = mediaJob?.state in setOf("Starting", "Downloading", "Queued") &&
        MediaDownloadService.activeJobId != null
    val qualityLabels = mapOf(
        "best" to "Best available video",
        "720" to "Up to 720p",
        "480" to "Up to 480p",
        "audio" to "MP3 audio only"
    )
    Surface(modifier = Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
        Column(
            modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Spacer(Modifier.height(10.dp))
            Text("FastDownloader", style = MaterialTheme.typography.headlineLarge,
                color = MaterialTheme.colorScheme.primary)
            Text("Android media and file downloader · ALPHA TEST",
                style = MaterialTheme.typography.titleSmall)
            Text("Paste a secure link or share one from another app.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)

            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                FilterChip(
                    selected = mediaMode,
                    onClick = { mediaMode = true },
                    label = { Text("Video / audio") }
                )
                FilterChip(
                    selected = !mediaMode,
                    onClick = { mediaMode = false },
                    label = { Text("Direct file") }
                )
            }

            OutlinedTextField(
                value = url,
                onValueChange = { url = it },
                label = { Text("HTTPS link") },
                placeholder = { Text("https://...") },
                minLines = 2,
                maxLines = 4,
                modifier = Modifier.fillMaxWidth(),
                isError = url.isNotBlank() && !UrlTools.isHttps(url)
            )

            if (mediaMode) {
                Box {
                    OutlinedButton(onClick = { qualityMenu = true },
                        modifier = Modifier.fillMaxWidth()) {
                        Text("Quality: ${qualityLabels[quality] ?: quality}")
                    }
                    DropdownMenu(expanded = qualityMenu, onDismissRequest = { qualityMenu = false }) {
                        for ((key, label) in qualityLabels) {
                            DropdownMenuItem(
                                text = { Text(label) },
                                onClick = { quality = key; qualityMenu = false }
                            )
                        }
                    }
                }
            }

            Button(
                onClick = {
                    if (!UrlTools.isHttps(url)) {
                        message = "Enter a valid HTTPS URL first."
                    } else if (mediaMode) {
                        if (busy) {
                            message = "Pause the current video task before starting another."
                        } else {
                            JobStore.save(context, MediaJob(
                                id = UUID.randomUUID().toString(),
                                url = url.trim(),
                                quality = quality
                            ))
                            mediaJob = JobStore.read(context)
                            MediaDownloadService.send(context, MediaDownloadService.START)
                            message = "Starting media download. Keep your notification permissions enabled."
                        }
                    } else {
                        queueDirectDownload(context, url).fold(
                            onSuccess = {
                                directInfo = "Queued in Android Downloads"
                                message = "Direct download queued. Android handles the transfer."
                            },
                            onFailure = { message = it.message ?: "Could not queue direct download." }
                        )
                    }
                },
                enabled = url.isNotBlank() && !(mediaMode && busy),
                shape = RoundedCornerShape(14.dp),
                modifier = Modifier.fillMaxWidth()
            ) { Text(if (mediaMode) "Download video / audio" else "Download direct file") }

            if (message.isNotBlank()) Text(message,
                color = MaterialTheme.colorScheme.secondary,
                style = MaterialTheme.typography.bodySmall)

            mediaJob?.let { job ->
                HorizontalDivider()
                Text("Media task", style = MaterialTheme.typography.titleMedium)
                Text("${job.state} · ${qualityLabels[job.quality] ?: job.quality}",
                    color = MaterialTheme.colorScheme.primary)
                LinearProgressIndicator(progress = { job.progress / 100f },
                    modifier = Modifier.fillMaxWidth())
                Text("${job.progress}% · ${job.detail}",
                    style = MaterialTheme.typography.bodySmall)
                if (job.state in setOf("Downloading", "Starting") && busy) {
                    OutlinedButton(
                        onClick = {
                            MediaDownloadService.send(context, MediaDownloadService.PAUSE)
                            message = "Pausing. Partial data will be kept for resume."
                        },
                        modifier = Modifier.fillMaxWidth()
                    ) { Text("Pause video task") }
                } else if (job.state in setOf("Paused", "Failed", "Starting", "Downloading")) {
                    OutlinedButton(
                        onClick = {
                            MediaDownloadService.send(context, MediaDownloadService.RESUME)
                            message = "Attempting to continue partial download."
                        },
                        modifier = Modifier.fillMaxWidth()
                    ) { Text("Resume video task") }
                }
            }

            HorizontalDivider()
            Text("Latest direct file", style = MaterialTheme.typography.titleMedium)
            Text(directInfo)
            OutlinedButton(
                modifier = Modifier.fillMaxWidth(),
                onClick = { context.startActivity(Intent(DownloadManager.ACTION_VIEW_DOWNLOADS)) }
            ) { Text("Open Android Downloads") }

            HorizontalDivider()
            Text("Alpha limits: one video at a time. Resume depends on the source server. " +
                "Some sites require login, block extraction, or use DRM and are not supported. " +
                "No playlists, cookies, scheduled jobs or in-app download history yet. " +
                "Test on an Android phone before treating this as a stable release.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(16.dp))
        }
    }
}
