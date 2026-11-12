package com.zaxmill.fastdownloader

import android.content.Context
import org.json.JSONObject

internal data class MediaJob(
    val id: String,
    val url: String,
    val quality: String,
    val state: String = "Queued",
    val progress: Int = 0,
    val detail: String = ""
)

/** Only one concurrent video task in the alpha; preserves partial files for resume. */
internal object JobStore {
    private const val PREF = "mobile_jobs"
    fun read(context: Context): MediaJob? = runCatching {
        val raw = context.getSharedPreferences(PREF, Context.MODE_PRIVATE).getString("media", null)
            ?: return null
        val o = JSONObject(raw)
        MediaJob(
            o.getString("id"), o.getString("url"), o.getString("quality"),
            o.getString("state"), o.optInt("progress"), o.optString("detail")
        )
    }.getOrNull()

    @Synchronized
    fun save(context: Context, job: MediaJob) {
        val o = JSONObject()
            .put("id", job.id).put("url", job.url).put("quality", job.quality)
            .put("state", job.state).put("progress", job.progress)
            .put("detail", job.detail.take(250))
        context.getSharedPreferences(PREF, Context.MODE_PRIVATE)
            .edit().putString("media", o.toString()).apply()
    }

    fun directId(context: Context): Long =
        context.getSharedPreferences(PREF, Context.MODE_PRIVATE).getLong("direct_id", -1L)

    fun saveDirect(context: Context, id: Long) {
        context.getSharedPreferences(PREF, Context.MODE_PRIVATE).edit()
            .putLong("direct_id", id).apply()
    }
}
