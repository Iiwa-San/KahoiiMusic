package com.kahoii.music.model

enum class LoopMode {
    OFF,        // Phát tuần tự, hết danh sách thì dừng
    ALL,        // Lặp toàn bộ danh sách
    ONE,        // Lặp 1 bài hiện tại
    SHUFFLE     // Phát ngẫu nhiên các bài trong list, hết thì lặp lại
}
