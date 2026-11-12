package com.zaxmill.fastdownloader

import java.net.URI

/** Accept only secure public-looking URLs; no credentials or malformed authorities. */
internal object UrlTools {
    private val link = Regex("""https?://[^\s<>"']+""", RegexOption.IGNORE_CASE)

    fun fromSharedText(text: String): String? =
        link.find(text)?.value?.trimEnd('.', ',', ')', ']', '}', ';', '!', '?')
            ?.takeIf { isHttps(it) }

    fun isHttps(raw: String): Boolean = runCatching {
        val u = URI(raw.trim())
        u.scheme.equals("https", ignoreCase = true) &&
            u.host != null &&
            u.host.isNotBlank() &&
            u.userInfo == null &&
            u.rawFragment == null &&
            (u.port == -1 || u.port in 1..65535) &&
            !u.host.equals("localhost", ignoreCase = true) &&
            !u.host.endsWith(".local", ignoreCase = true) &&
            !u.host.endsWith(".localhost", ignoreCase = true) &&
            !u.host.contains(":") &&
            !u.host.all { it.isDigit() } &&
            !isPrivateIpv4(u.host)
    }.getOrDefault(false)

    private fun isPrivateIpv4(host: String): Boolean {
        val octets = host.split('.')
        if (octets.size != 4 || octets.any { it.toIntOrNull() !in 0..255 }) return false
        val n = octets.map { it.toInt() }
        return n[0] == 0 || n[0] == 10 || n[0] == 127 ||
            (n[0] == 169 && n[1] == 254) ||
            (n[0] == 172 && n[1] in 16..31) ||
            (n[0] == 192 && n[1] == 168) ||
            n[0] >= 224
    }
}
