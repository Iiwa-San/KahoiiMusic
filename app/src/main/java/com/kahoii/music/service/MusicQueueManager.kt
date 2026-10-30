package com.kahoii.music.service

object MusicQueueManager {
    @Volatile
    var onNextAction: (() -> Unit)? = null

    @Volatile
    var onPreviousAction: (() -> Unit)? = null

    fun triggerNext() {
        onNextAction?.invoke()
    }

    fun triggerPrevious() {
        onPreviousAction?.invoke()
    }
}

