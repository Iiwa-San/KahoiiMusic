package com.kahoii.music.model

enum class SleepTimerOption(val minutes: Int, val label: String) {
    OFF(0, "Tắt hẹn giờ"),
    MIN_5(5, "5 phút"),
    MIN_10(10, "10 phút"),
    MIN_15(15, "15 phút"),
    MIN_20(20, "20 phút"),
    MIN_30(30, "30 phút"),
    MIN_45(45, "45 phút"),
    MIN_60(60, "60 phút"),
    END_OF_TRACK(-1, "Hết bài hát hiện tại");

    val isEndOfTrack: Boolean
        get() = this == END_OF_TRACK

    val isActive: Boolean
        get() = this != OFF
}

