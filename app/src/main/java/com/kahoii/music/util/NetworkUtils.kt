package com.kahoii.music.util

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities

enum class NetworkType {
    WIFI,
    CELLULAR,
    NONE
}

object NetworkUtils {
    fun getNetworkType(context: Context): NetworkType {
        val cm = context.getSystemService(Context.CONNECTIVITY_SERVICE) as? ConnectivityManager
            ?: return NetworkType.NONE
        val activeNetwork = cm.activeNetwork ?: return NetworkType.NONE
        val caps = cm.getNetworkCapabilities(activeNetwork) ?: return NetworkType.NONE
        return when {
            caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) ||
            caps.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) -> NetworkType.WIFI
            caps.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) -> NetworkType.CELLULAR
            else -> NetworkType.NONE
        }
    }

    fun isCellular(context: Context): Boolean = getNetworkType(context) == NetworkType.CELLULAR
    fun isConnected(context: Context): Boolean = getNetworkType(context) != NetworkType.NONE
}
