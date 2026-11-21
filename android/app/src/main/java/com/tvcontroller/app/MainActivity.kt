package com.tvcontroller.app

import android.os.Bundle
import android.view.View
import android.widget.Button
import android.widget.ProgressBar
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

class MainActivity : AppCompatActivity() {
    private lateinit var client: TvClient
    private lateinit var statusText: TextView
    private lateinit var logText: TextView
    private lateinit var busyRing: ProgressBar
    private lateinit var buttons: List<Button>
    private var job: Job? = null
    private val logBuilder = StringBuilder()

    override fun onCreate(savedInstanceState: android.os.Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        statusText = findViewById(R.id.statusText)
        logText = findViewById(R.id.logText)
        busyRing = findViewById(R.id.busyRing)
        buttons = listOf(
            findViewById(R.id.btnOpenBili),
            findViewById(R.id.btnSleep),
            findViewById(R.id.btnBack),
            findViewById(R.id.btnHome),
            findViewById(R.id.btnVolumeDown),
            findViewById(R.id.btnMute),
            findViewById(R.id.btnVolumeUp),
        )

        client = TvClient { appendLog(it) }

        findViewById<Button>(R.id.btnOpenBili).setOnClickListener { run { client.openBilibili(this) } }
        findViewById<Button>(R.id.btnSleep).setOnClickListener { run { client.sleep(this) } }
        findViewById<Button>(R.id.btnBack).setOnClickListener { run { client.back(this) } }
        findViewById<Button>(R.id.btnHome).setOnClickListener { run { client.home(this) } }
        findViewById<Button>(R.id.btnVolumeDown).setOnClickListener { run { client.volumeDown(this) } }
        findViewById<Button>(R.id.btnMute).setOnClickListener { run { client.mute(this) } }
        findViewById<Button>(R.id.btnVolumeUp).setOnClickListener { run { client.volumeUp(this) } }
    }

    private fun run(action: suspend () -> String) {
        if (job?.isActive == true) return
        setBusy(true, "执行中…")
        job = lifecycleScope.launch {
            try {
                statusText.text = action()
            } catch (e: Exception) {
                statusText.text = "失败：${e.message}"
                appendLog("ERROR ${e.message}")
            } finally {
                setBusy(false, null)
                delay(2500)
                if (job?.isActive != true) statusText.text = getString(R.string.ready)
            }
        }
    }

    private fun setBusy(busy: Boolean, status: String?) {
        buttons.forEach { it.isEnabled = !busy }
        busyRing.visibility = if (busy) View.VISIBLE else View.GONE
        status?.let { statusText.text = it }
    }

    private fun appendLog(line: String) {
        logBuilder.append(line).append('\n')
        logText.text = logBuilder.toString()
    }
}
