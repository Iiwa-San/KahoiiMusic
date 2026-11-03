package com.kahoii.music.model

import com.google.gson.annotations.SerializedName
import java.io.Serializable
import java.util.UUID

data class UserPlaylist(
    @SerializedName("id", alternate = ["Id"])
    val id: String = UUID.randomUUID().toString(),

    @SerializedName("name", alternate = ["Name"])
    val name: String = "",

    @SerializedName("createdAt", alternate = ["CreatedAt", "created", "Created"])
    val createdAt: Long = System.currentTimeMillis(),

    @SerializedName("items", alternate = ["Items"])
    val items: List<PlaylistItem> = emptyList()
) : Serializable
