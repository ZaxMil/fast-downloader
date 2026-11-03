package com.zaxmill.fastdownloader

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import java.net.URI

class MainActivity : ComponentActivity() {
    private val incomingLink = mutableStateOf("")

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        acceptLink(intent)
        setContent {
            val dark = Color(0xFF0B1019)
            MaterialTheme(colorScheme = darkColorScheme(
                background = dark,
                surface = Color(0xFF141D2B),
                primary = Color(0xFF53E6AD),
                onBackground = Color.White
            )) {
                DownloadHome(incomingLink.value)
            }
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
        val link = Regex("""https?://[^\s<>"]+""").find(text)?.value ?: return
        incomingLink.value = link
    }
}

@Composable
private fun DownloadHome(sharedLink: String) {
    var url by remember { mutableStateOf(sharedLink) }
    var status by remember { mutableStateOf("") }
    LaunchedEffect(sharedLink) { if (sharedLink.isNotBlank()) url = sharedLink }
    Surface(modifier = Modifier.fillMaxSize()) {
        Column(
            modifier = Modifier.fillMaxSize().padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(18.dp)
        ) {
            Spacer(Modifier.height(22.dp))
            Text("FastDownloader", style = MaterialTheme.typography.headlineMedium)
            Text("Android preview · Foundation", color = MaterialTheme.colorScheme.primary)
            Text("Paste a link or share it from another app.")
            OutlinedTextField(
                value = url, onValueChange = { url = it },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("Download URL") },
                singleLine = false,
                minLines = 2
            )
            Button(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(12.dp),
                onClick = {
                    status = try {
                        val parsed = URI(url.trim())
                        if (parsed.scheme !in listOf("https", "http") ||
                            parsed.host.isNullOrBlank() || parsed.userInfo != null) {
                            "Enter a valid HTTP or HTTPS URL."
                        } else {
                            "Valid link. Download engine is not connected yet."
                        }
                    } catch (_: Exception) {
                        "Enter a valid HTTP or HTTPS URL."
                    }
                }
            ) { Text("Check link") }
            if (status.isNotBlank()) Text(status)
            Text(
                "This is an early app shell, not a working video downloader yet.",
                style = MaterialTheme.typography.bodySmall
            )
        }
    }
}
