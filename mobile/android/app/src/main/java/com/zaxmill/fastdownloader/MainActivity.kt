package com.zaxmill.fastdownloader

import android.app.DownloadManager
import android.content.Context
import android.content.Intent
import android.net.Uri
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
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import java.net.URI

class MainActivity : ComponentActivity() {
    private val incomingLink = mutableStateOf("")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        acceptLink(intent)
        setContent {
            MaterialTheme(colorScheme = darkColorScheme(
                background = Color(0xFF0B1019),
                surface = Color(0xFF141D2B),
                primary = Color(0xFF53E6AD)
            )) { DownloadHome(incomingLink.value) }
        }
    }
    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        acceptLink(intent)
    }
    private fun acceptLink(intent: Intent?) {
        val text = when (intent?.action) {
            Intent.ACTION_SEND -> intent.getStringExtra(Intent.EXTRA_TEXT)
            Intent.ACTION_VIEW -> intent.dataString
            else -> null
        } ?: return
        incomingLink.value = Regex("""https?://[^\\s<>"]+""").find(text)?.value ?: ""
    }
}

/** The Android OS owns transfers after enqueue; they can continue after this Activity closes. */
internal fun queueDirectDownload(context: Context, input: String): Result<Long> = runCatching {
    val uri = URI(input.trim())
    require(uri.scheme == "https" && !uri.host.isNullOrBlank() && uri.userInfo == null) {
        "Only public HTTPS links are accepted in this preview."
    }
    val lastSegment = uri.path?.substringAfterLast('/')?.takeIf { it.isNotBlank() } ?: "download"
    val filename = Uri.decode(lastSegment)
        .replace(Regex("""[\\/\\u0000-\\u001f\\u007f]"""), "_")
        .take(100).ifBlank { "download" }
    val request = DownloadManager.Request(Uri.parse(uri.toASCIIString()))
        .setTitle(filename)
        .setDescription("FastDownloader direct file")
        .setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED)
        .setDestinationInExternalPublicDir(Environment.DIRECTORY_DOWNLOADS, filename)
        .setAllowedOverMetered(true)
        .setAllowedOverRoaming(false)
    (context.getSystemService(Context.DOWNLOAD_SERVICE) as DownloadManager).enqueue(request)
}

@Composable
private fun DownloadHome(sharedLink: String) {
    val context = LocalContext.current
    var url by remember { mutableStateOf(sharedLink) }
    var status by remember { mutableStateOf("") }
    var lastId by remember { mutableStateOf<Long?>(null) }
    LaunchedEffect(sharedLink) { if (sharedLink.isNotBlank()) url = sharedLink }
    Surface(modifier = Modifier.fillMaxSize()) {
        Column(
            modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(18.dp)
        ) {
            Spacer(Modifier.height(22.dp))
            Text("FastDownloader", style = MaterialTheme.typography.headlineMedium)
            Text("Android · Direct download alpha", color = MaterialTheme.colorScheme.primary)
            Text("Paste a direct HTTPS file link, or share one from another app.")
            OutlinedTextField(
                value = url, onValueChange = { url = it },
                modifier = Modifier.fillMaxWidth(), label = { Text("File URL") },
                minLines = 2
            )
            Button(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                onClick = {
                    queueDirectDownload(context, url).fold(
                        onSuccess = { id ->
                            lastId = id
                            status = "Queued in Android Downloads (ID $id)."
                        },
                        onFailure = { status = it.message ?: "Could not start download." }
                    )
                }
            ) { Text("Download file") }
            OutlinedButton(
                modifier = Modifier.fillMaxWidth(),
                onClick = {
                    context.startActivity(Intent(DownloadManager.ACTION_VIEW_DOWNLOADS))
                }
            ) { Text("Open downloads") }
            lastId?.let { id ->
                OutlinedButton(
                    modifier = Modifier.fillMaxWidth(),
                    onClick = {
                        val manager = context.getSystemService(Context.DOWNLOAD_SERVICE) as DownloadManager
                        manager.query(DownloadManager.Query().setFilterById(id))?.use { cursor ->
                            status = if (cursor.moveToFirst()) {
                                val state = cursor.getInt(cursor.getColumnIndexOrThrow(DownloadManager.COLUMN_STATUS))
                                when (state) {
                                    DownloadManager.STATUS_SUCCESSFUL -> "Download complete."
                                    DownloadManager.STATUS_FAILED -> "Download failed. Check Android Downloads."
                                    DownloadManager.STATUS_RUNNING -> "Downloading…"
                                    DownloadManager.STATUS_PAUSED -> "Paused by Android; waiting for network."
                                    else -> "Waiting in queue…"
                                }
                            } else "Download no longer exists."
                        }
                    }
                ) { Text("Check last download") }
            }
            if (status.isNotBlank()) Text(status)
            Text(
                "Direct HTTPS files only. Video-page extraction, manual pause/resume and yt-dlp are not available yet. Android manages network retries and notifications.",
                style = MaterialTheme.typography.bodySmall
            )
        }
    }
}
