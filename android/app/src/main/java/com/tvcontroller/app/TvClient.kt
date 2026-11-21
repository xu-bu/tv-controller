package com.tvcontroller.app

import android.content.Context
import com.flyfishxu.kadb.Kadb
import com.flyfishxu.kadb.cert.KadbCert
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

class TvClient(private val log: (String) -> Unit) {
    companion object {
        private const val HOST = "192.168.3.126"
        private const val PORT = 5555
        private const val BILIBILI = "com.xiaodianshi.tv.yst"
    }

    private var identityReady = false

    private fun writeLog(message: String) {
        log("[${java.time.LocalTime.now()}] $message")
    }

    private fun ensureIdentity(context: Context) {
        if (identityReady) return
        try {
            // 打包的 adbkey 是电视已授权的主机密钥，导入后免在电视上二次授权。
            context.assets.open("adbkey").use { input ->
                KadbCert.importPrivateKey(input.readBytes())
            }
            identityReady = true
            writeLog("已导入主机密钥")
        } catch (e: Exception) {
            writeLog("导入密钥失败，使用默认身份：${e.message}")
            identityReady = true
        }
    }

    private suspend fun <T> withKadb(context: Context, block: (Kadb) -> T): T =
        withContext(Dispatchers.IO) {
            ensureIdentity(context)
            Kadb.create(HOST, PORT).use { block(it) }
        }

    private suspend fun shell(context: Context, command: String): String =
        withKadb(context) { kadb ->
            val response = kadb.shell(command)
            response.output
        }

    private suspend fun isOnline(context: Context): Boolean = try {
        shell(context, "echo ok").contains("ok")
    } catch (e: Exception) {
        false
    }

    suspend fun openBilibili(context: Context): String = try {
        writeLog("连接 $HOST")
        if (!isOnline(context)) {
            throw IllegalStateException("无法连接电视（可能深度待机或断电，需红外遥控器开机）")
        }
        writeLog("启动哔哩哔哩 $BILIBILI")
        shell(context, "am start -n $BILIBILI/.ui.main.MainActivity")
        var entered = false
        repeat(10) {
            kotlinx.coroutines.delay(300)
            val dump = shell(context, "dumpsys window")
            if (isForeground(dump, BILIBILI)) entered = true
        }
        if (entered) "已进入哔哩哔哩" else "启动指令已发送，请查看电视屏幕"
    } catch (e: Exception) {
        throw IllegalStateException(e.message ?: "打开哔哩哔哩失败", e)
    }

    suspend fun sleep(context: Context): String = try {
        writeLog("连接 $HOST")
        if (!isOnline(context)) {
            throw IllegalStateException("无法连接电视，无法执行待机")
        }
        val initial = shell(context, "dumpsys window")
        if (initial.contains("SoundBoxDefaultActivity")) {
            return "电视已处于待机状态"
        }
        writeLog("发送电源键，打开电源菜单")
        shell(context, "input keyevent 26")
        var dialog = false
        repeat(20) {
            kotlinx.coroutines.delay(200)
            if (shell(context, "dumpsys window").contains("shutdowndialog")) dialog = true
        }
        if (!dialog) return "未能打开电源菜单，请查看电视屏幕"
        writeLog("确认熄屏")
        shell(context, "input keyevent 66")
        repeat(20) {
            kotlinx.coroutines.delay(400)
            if (shell(context, "dumpsys window").contains("SoundBoxDefaultActivity")) {
                writeLog("已进入小程序待机")
                return "电视已进入待机（网络保持在线）"
            }
        }
        "未能确认电视进入待机，请查看电视屏幕"
    } catch (e: Exception) {
        throw IllegalStateException(e.message ?: "待机失败", e)
    }

    suspend fun back(context: Context) = sendKey(context, 4, "返回键")
    suspend fun home(context: Context) = sendKey(context, 3, "主页键")
    suspend fun volumeUp(context: Context) = sendKey(context, 24, "音量+")
    suspend fun volumeDown(context: Context) = sendKey(context, 25, "音量-")
    suspend fun mute(context: Context) = sendKey(context, 113, "静音切换")

    private suspend fun sendKey(context: Context, keyCode: Int, name: String): String = try {
        writeLog("发送$name")
        shell(context, "input keyevent $keyCode")
        "$name 已发送"
    } catch (e: Exception) {
        throw IllegalStateException(e.message ?: "$name 发送失败", e)
    }

    private fun isForeground(dump: String, packageName: String): Boolean =
        dump.lines().any { it.contains("mCurrentFocus") && it.contains(packageName) }
}
