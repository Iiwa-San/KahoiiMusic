package com.kahoii.music.ui.components

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.SignalCellularAlt
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.kahoii.music.ui.theme.NeonPink

@Composable
fun CellularDownloadDialog(
    trackTitle: String? = null,
    onConfirm: () -> Unit,
    onDismiss: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        shape = RoundedCornerShape(24.dp),
        containerColor = Color(0xFF1E1730),
        icon = {
            Icon(
                imageVector = Icons.Default.SignalCellularAlt,
                contentDescription = "Cellular",
                tint = Color(0xFFF59E0B)
            )
        },
        title = {
            Text(
                text = "Cảnh Báo Dữ Liệu Di Động",
                color = Color.White,
                fontSize = 17.sp,
                fontWeight = FontWeight.Bold
            )
        },
        text = {
            Text(
                text = if (trackTitle.isNullOrBlank()) {
                    "Bạn đang kết nối dữ liệu di động (4G/5G). Quá trình tải nhạc có thể làm tiêu tốn dung lượng gói cước. Bạn có muốn tiếp tục tải không?"
                } else {
                    "Bạn đang kết nối dữ liệu di động (4G/5G). Tải bài hát \"$trackTitle\" có thể làm tiêu tốn dung lượng gói cước. Bạn có muốn tiếp tục?"
                },
                color = Color.White.copy(alpha = 0.8f),
                fontSize = 13.5.sp
            )
        },
        confirmButton = {
            Button(
                onClick = onConfirm,
                colors = ButtonDefaults.buttonColors(containerColor = NeonPink),
                shape = RoundedCornerShape(12.dp)
            ) {
                Text("Tiếp tục tải", fontWeight = FontWeight.Bold)
            }
        },
        dismissButton = {
            OutlinedButton(
                onClick = onDismiss,
                shape = RoundedCornerShape(12.dp)
            ) {
                Text("Hủy", color = Color.White.copy(alpha = 0.7f))
            }
        }
    )
}
