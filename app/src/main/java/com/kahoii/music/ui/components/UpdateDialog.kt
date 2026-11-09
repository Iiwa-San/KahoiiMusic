package com.kahoii.music.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CloudDownload
import androidx.compose.material.icons.filled.Done
import androidx.compose.material.icons.filled.NewReleases
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import com.kahoii.music.model.AppUpdateInfo
import com.kahoii.music.model.UpdateDownloadState
import com.kahoii.music.ui.theme.NeonPink
import java.io.File
import java.util.Locale

@Composable
fun UpdateDialog(
    updateInfo: AppUpdateInfo,
    downloadState: UpdateDownloadState,
    onStartDownload: () -> Unit,
    onInstall: (File) -> Unit,
    onDismiss: () -> Unit
) {
    val isDownloading = downloadState is UpdateDownloadState.Downloading
    val isCompleted = downloadState is UpdateDownloadState.Completed
    val isError = downloadState is UpdateDownloadState.Error

    Dialog(
        onDismissRequest = {
            if (!updateInfo.forceUpdate && !isDownloading) {
                onDismiss()
            }
        },
        properties = DialogProperties(
            dismissOnBackPress = !updateInfo.forceUpdate && !isDownloading,
            dismissOnClickOutside = !updateInfo.forceUpdate && !isDownloading
        )
    ) {
        Surface(
            shape = RoundedCornerShape(22.dp),
            color = Color(0xFF181326),
            tonalElevation = 8.dp,
            modifier = Modifier
                .fillMaxWidth()
                .border(
                    width = 1.dp,
                    brush = Brush.verticalGradient(
                        listOf(
                            NeonPink.copy(alpha = 0.6f),
                            Color(0xFF8B5CF6).copy(alpha = 0.2f),
                            Color.Transparent
                        )
                    ),
                    shape = RoundedCornerShape(22.dp)
                )
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(22.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                // Icon Header
                Box(
                    modifier = Modifier
                        .size(56.dp)
                        .clip(CircleShape)
                        .background(
                            Brush.linearGradient(
                                listOf(NeonPink.copy(alpha = 0.25f), Color(0xFF7C3AED).copy(alpha = 0.25f))
                            )
                        )
                        .border(1.dp, NeonPink.copy(alpha = 0.5f), CircleShape),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = if (isCompleted) Icons.Default.Done else Icons.Default.NewReleases,
                        contentDescription = "Cập nhật ứng dụng",
                        tint = if (isCompleted) Color(0xFF4ADE80) else NeonPink,
                        modifier = Modifier.size(30.dp)
                    )
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Title
                Text(
                    text = if (isCompleted) "Tải Cập Nhật Thành Công!" else "Đã Có Bản Cập Nhật Mới!",
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Bold,
                    color = Color.White
                )

                Spacer(modifier = Modifier.height(4.dp))

                // Version Badge
                Box(
                    modifier = Modifier
                        .clip(RoundedCornerShape(8.dp))
                        .background(NeonPink.copy(alpha = 0.15f))
                        .padding(horizontal = 10.dp, vertical = 3.dp)
                ) {
                    Text(
                        text = "Phiên bản v${updateInfo.versionName}",
                        fontSize = 12.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = NeonPink
                    )
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Changelog content or Download progress
                when (downloadState) {
                    is UpdateDownloadState.Idle -> {
                        if (updateInfo.changelog.isNotBlank()) {
                            Text(
                                text = "Nội dung cập nhật:",
                                fontSize = 13.sp,
                                fontWeight = FontWeight.SemiBold,
                                color = Color.White.copy(alpha = 0.9f),
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(bottom = 6.dp)
                            )
                            Box(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .heightIn(max = 180.dp)
                                    .clip(RoundedCornerShape(12.dp))
                                    .background(Color.White.copy(alpha = 0.05f))
                                    .padding(12.dp)
                            ) {
                                Column(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .verticalScroll(rememberScrollState())
                                ) {
                                    Text(
                                        text = updateInfo.changelog,
                                        fontSize = 13.sp,
                                        lineHeight = 19.sp,
                                        color = Color(0xFFD1D5DB)
                                    )
                                }
                            }
                        }
                    }

                    is UpdateDownloadState.Downloading -> {
                        val progress = downloadState.progress
                        val downloadedMB = downloadState.downloadedBytes.toFloat() / (1024 * 1024)
                        val totalMB = downloadState.totalBytes.toFloat() / (1024 * 1024)
                        val percentInt = (progress * 100).toInt()

                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 8.dp),
                            horizontalAlignment = Alignment.CenterHorizontally
                        ) {
                            Text(
                                text = "Đang tải bản cập nhật...",
                                fontSize = 13.sp,
                                color = Color.White.copy(alpha = 0.8f)
                            )
                            Spacer(modifier = Modifier.height(10.dp))

                            LinearProgressIndicator(
                                progress = { progress },
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(8.dp)
                                    .clip(RoundedCornerShape(4.dp)),
                                color = NeonPink,
                                trackColor = Color.White.copy(alpha = 0.1f),
                                strokeCap = StrokeCap.Round
                            )

                            Spacer(modifier = Modifier.height(8.dp))

                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween
                            ) {
                                Text(
                                    text = "$percentInt%",
                                    fontSize = 12.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = NeonPink
                                )
                                Text(
                                    text = if (totalMB > 0) {
                                        String.format(Locale.US, "%.1f MB / %.1f MB", downloadedMB, totalMB)
                                    } else {
                                        String.format(Locale.US, "%.1f MB", downloadedMB)
                                    },
                                    fontSize = 12.sp,
                                    color = Color.White.copy(alpha = 0.6f)
                                )
                            }
                        }
                    }

                    is UpdateDownloadState.Completed -> {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(12.dp))
                                .background(Color(0xFF22C55E).copy(alpha = 0.1f))
                                .border(1.dp, Color(0xFF22C55E).copy(alpha = 0.3f), RoundedCornerShape(12.dp))
                                .padding(14.dp),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = "Bản cập nhật đã sẵn sàng! Nhấn 'Cài đặt ngay' để hoàn tất nâng cấp mà không mất dữ liệu của bạn.",
                                fontSize = 13.sp,
                                lineHeight = 18.sp,
                                color = Color(0xFF86EFAC),
                                textAlign = androidx.compose.ui.text.style.TextAlign.Center
                            )
                        }
                    }

                    is UpdateDownloadState.Error -> {
                        Box(
                            modifier = Modifier
                                .fillMaxWidth()
                                .clip(RoundedCornerShape(12.dp))
                                .background(Color(0xFFEF4444).copy(alpha = 0.12f))
                                .border(1.dp, Color(0xFFEF4444).copy(alpha = 0.3f), RoundedCornerShape(12.dp))
                                .padding(12.dp),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = "Lỗi khi tải cập nhật: ${downloadState.message}",
                                fontSize = 12.5.sp,
                                color = Color(0xFFFCA5A5),
                                textAlign = androidx.compose.ui.text.style.TextAlign.Center
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(18.dp))

                // Action Buttons
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    if (!updateInfo.forceUpdate && !isDownloading) {
                        OutlinedButton(
                            onClick = onDismiss,
                            modifier = Modifier
                                .weight(1f)
                                .height(44.dp),
                            shape = RoundedCornerShape(12.dp),
                            colors = ButtonDefaults.outlinedButtonColors(
                                contentColor = Color.White.copy(alpha = 0.7f)
                            ),
                            border = androidx.compose.foundation.BorderStroke(1.dp, Color.White.copy(alpha = 0.2f))
                        ) {
                            Text(
                                text = if (isCompleted) "Đóng" else "Để sau",
                                fontSize = 13.sp,
                                fontWeight = FontWeight.Medium
                            )
                        }
                    }

                    Button(
                        onClick = {
                            when (downloadState) {
                                is UpdateDownloadState.Completed -> {
                                    onInstall(downloadState.apkFile)
                                }
                                is UpdateDownloadState.Error -> {
                                    onStartDownload()
                                }
                                else -> {
                                    onStartDownload()
                                }
                            }
                        },
                        enabled = !isDownloading,
                        modifier = Modifier
                            .weight(1f)
                            .height(44.dp),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.buttonColors(
                            containerColor = if (isCompleted) Color(0xFF16A34A) else NeonPink
                        )
                    ) {
                        when (downloadState) {
                            is UpdateDownloadState.Downloading -> {
                                CircularProgressIndicator(
                                    modifier = Modifier.size(18.dp),
                                    strokeWidth = 2.dp,
                                    color = Color.White
                                )
                                Spacer(modifier = Modifier.width(6.dp))
                                Text("Đang tải...", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                            }
                            is UpdateDownloadState.Completed -> {
                                Icon(Icons.Default.CloudDownload, contentDescription = null, modifier = Modifier.size(18.dp))
                                Spacer(modifier = Modifier.width(6.dp))
                                Text("Cài đặt ngay", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                            }
                            is UpdateDownloadState.Error -> {
                                Icon(Icons.Default.Refresh, contentDescription = null, modifier = Modifier.size(18.dp))
                                Spacer(modifier = Modifier.width(6.dp))
                                Text("Thử lại", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                            }
                            else -> {
                                Text("Cập nhật ngay 🚀", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                            }
                        }
                    }
                }

                Spacer(modifier = Modifier.height(10.dp))

                val context = androidx.compose.ui.platform.LocalContext.current
                Text(
                    text = "Tải trực tiếp qua trình duyệt ↗",
                    color = Color.White.copy(alpha = 0.5f),
                    fontSize = 11.5.sp,
                    fontWeight = FontWeight.Medium,
                    modifier = Modifier
                        .clickable {
                            try {
                                val intent = android.content.Intent(
                                    android.content.Intent.ACTION_VIEW,
                                    android.net.Uri.parse(updateInfo.apkUrl)
                                )
                                context.startActivity(intent)
                            } catch (_: Exception) {}
                        }
                        .padding(horizontal = 8.dp, vertical = 4.dp)
                )
            }
        }
    }
}

