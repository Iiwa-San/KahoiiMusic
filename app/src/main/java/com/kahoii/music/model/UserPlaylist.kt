package com.kahoii.music.model

import com.google.gson.annotations.SerializedName
import java.io.Serializable
import java.util.UUID

data class UserPlaylist(
    @SerializedName("id")
    val id: String = UUID.randomUUID().toString(),

    @SerializedName("name")
    var name: String = "",

    @SerializedName("createdAt")
    val createdAt: Long = System.currentTimeMillis(),

    @SerializedName("items")
    val items: MutableList<PlaylistItem> = mutableListOf()
) : Serializable
