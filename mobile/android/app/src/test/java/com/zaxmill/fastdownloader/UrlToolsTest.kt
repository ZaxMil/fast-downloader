package com.zaxmill.fastdownloader

import org.junit.Assert.*
import org.junit.Test

class UrlToolsTest {
    @Test fun acceptsPublicHttpsWithQuery() {
        assertTrue(UrlTools.isHttps("https://example.com/file.mp4?token=abc"))
        assertTrue(UrlTools.isHttps("HTTPS://video.example.org/watch?v=1"))
    }
    @Test fun rejectsUnsafeAndMalformedUrls() {
        for (url in listOf(
            "http://example.com", "javascript:alert(1)", "https://user:pass@example.com",
            "https://localhost/admin", "https://127.0.0.1/test",
            "https://192.168.0.1/", "https://10.0.0.2/a", "https://169.254.1.2/",
            "https://evil.local/a", "https://example.com/#secret",
            "https://", "file:///etc/passwd"
        )) assertFalse(url, UrlTools.isHttps(url))
    }
    @Test fun extractsSharedLinks() {
        assertEquals("https://example.org/watch?v=12",
            UrlTools.fromSharedText("Look at https://example.org/watch?v=12."))
        assertNull(UrlTools.fromSharedText("No URL here"))
        assertNull(UrlTools.fromSharedText("http://example.org/test"))
    }
}
