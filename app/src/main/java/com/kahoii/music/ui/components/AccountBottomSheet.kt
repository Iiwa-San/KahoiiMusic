package com.kahoii.music.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccountCircle
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.CloudDone
import androidx.compose.material.icons.filled.CloudSync
import androidx.compose.material.icons.filled.DownloadDone
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Logout
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import android.graphics.BitmapFactory
import androidx.compose.foundation.Image
import androidx.compose.foundation.border
import androidx.compose.material.icons.filled.Edit
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.TabRowDefaults
import androidx.compose.material3.TabRowDefaults.tabIndicatorOffset
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.platform.LocalContext
import android.widget.Toast
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.kahoii.music.data.FirebaseUser
import com.kahoii.music.ui.theme.NeonPink

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AccountBottomSheet(
    currentUser: FirebaseUser?,
    isSyncing: Boolean,
    syncStatusMessage: String?,
    savedTracksCount: Int = 0,
    collectionsCount: Int = 0,
    historyCount: Int = 0,
    onSignIn: (String, String) -> Unit,
    onSignUp: (String, String) -> Unit,
    onSignOut: () -> Unit,
    onSyncNow: () -> Unit,
    onForgotPassword: (String) -> Unit = {},
    onUpdateAvatar: (android.graphics.Bitmap) -> Unit = {},
    onDismissRequest: () -> Unit
) {
    val context = LocalContext.current
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    var selectedTab by remember { mutableIntStateOf(0) } // 0: Đăng nhập, 1: Đăng ký

    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var showPassword by remember { mutableStateOf(false) }
    var confirmPassword by remember { mutableStateOf("") }
    var showConfirmPassword by remember { mutableStateOf(false) }
    var localError by remember { mutableStateOf<String?>(null) }
    var showSignOutConfirm by remember { mutableStateOf(false) }
    var bitmapToCrop by remember { mutableStateOf<android.graphics.Bitmap?>(null) }
    var showCropDialog by remember { mutableStateOf(false) }

    val imagePickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri: android.net.Uri? ->
        if (uri != null) {
            try {
                context.contentResolver.openInputStream(uri)?.use { stream ->
                    val raw = BitmapFactory.decodeStream(stream)
                    if (raw != null) {
                        bitmapToCrop = raw
                        showCropDialog = true
                    }
                }
            } catch (e: Exception) {
                Toast.makeText(context, "Không thể đọc ảnh: ${e.message}", Toast.LENGTH_SHORT).show()
            }
        }
    }

    if (showCropDialog && bitmapToCrop != null) {
        ImageCropDialog(
            sourceBitmap = bitmapToCrop!!,
            onDismissRequest = {
                showCropDialog = false
                bitmapToCrop = null
            },
            onCropConfirmed = { cropped ->
                showCropDialog = false
                bitmapToCrop = null
                onUpdateAvatar(cropped)
            }
        )
    }

    if (showSignOutConfirm) {
        androidx.compose.material3.AlertDialog(
            onDismissRequest = { showSignOutConfirm = false },
            title = {
                Text(
                    text = "Đăng xuất",
                    color = Color.White,
                    fontWeight = FontWeight.Bold
                )
            },
            text = {
                Text(
                    text = "Bạn có muốn đăng xuất tài khoản không?\n\n(Danh sách bài hát của bạn đã được lưu an toàn trên đám mây và sẽ tự động tải về khi bạn đăng nhập lại).",
                    color = Color.White.copy(alpha = 0.85f),
                    fontSize = 13.5.sp,
                    lineHeight = 18.sp
                )
            },
            confirmButton = {
                Button(
                    onClick = {
                        showSignOutConfirm = false
                        onSignOut()
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFFF4B6E)),
                    shape = RoundedCornerShape(8.dp)
                ) {
                    Text("Đăng xuất", color = Color.White, fontWeight = FontWeight.Bold)
                }
            },
            dismissButton = {
                OutlinedButton(
                    onClick = { showSignOutConfirm = false },
                    shape = RoundedCornerShape(8.dp)
                ) {
                    Text("Hủy", color = Color.White)
                }
            },
            containerColor = Color(0xFF1E1830)
        )
    }

    ModalBottomSheet(
        onDismissRequest = onDismissRequest,
        sheetState = sheetState,
        containerColor = Color(0xFF140F24),
        dragHandle = null
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp, vertical = 16.dp)
        ) {
            // Drag Indicator
            Box(
                modifier = Modifier
                    .size(width = 36.dp, height = 4.dp)
                    .clip(RoundedCornerShape(2.dp))
                    .background(Color.White.copy(alpha = 0.25f))
                    .align(Alignment.CenterHorizontally)
            )

            Spacer(modifier = Modifier.height(14.dp))

            if (currentUser != null) {
                // ================= LOGGED IN VIEW (STREAMLINED LIKE PC) =================
                Column(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    val avatarBitmap = remember(currentUser.avatarBase64) {
                        com.kahoii.music.util.ImageUtils.base64ToBitmap(currentUser.avatarBase64)?.asImageBitmap()
                    }

                    Box(
                        modifier = Modifier
                            .size(76.dp)
                            .clickable {
                                imagePickerLauncher.launch("image/*")
                            },
                        contentAlignment = Alignment.Center
                    ) {
                        // Glowing gradient outer ring
                        Box(
                            modifier = Modifier
                                .size(72.dp)
                                .background(
                                    brush = Brush.linearGradient(
                                        colors = listOf(Color(0xFFFF2A85), Color(0xFF8A2BE2), Color(0xFF00E5FF))
                                    ),
                                    shape = CircleShape
                                )
                                .padding(2.dp)
                                .clip(CircleShape)
                                .background(Color(0xFF140F24)),
                            contentAlignment = Alignment.Center
                        ) {
                            if (avatarBitmap != null) {
                                Image(
                                    bitmap = avatarBitmap,
                                    contentDescription = "Avatar",
                                    contentScale = ContentScale.Crop,
                                    modifier = Modifier.fillMaxSize().clip(CircleShape)
                                )
                            } else {
                                Text(
                                    text = "👤",
                                    fontSize = 32.sp
                                )
                            }
                        }

                        // Pencil edit badge in bottom-right corner
                        Box(
                            modifier = Modifier
                                .size(24.dp)
                                .align(Alignment.BottomEnd)
                                .background(
                                    brush = Brush.linearGradient(
                                        colors = listOf(Color(0xFFFF2A85), Color(0xFF8A2BE2))
                                    ),
                                    shape = CircleShape
                                )
                                .border(2.dp, Color(0xFF140F24), CircleShape),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(
                                imageVector = Icons.Default.Edit,
                                contentDescription = "Sửa ảnh đại diện",
                                tint = Color.White,
                                modifier = Modifier.size(12.dp)
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(10.dp))

                    Text(
                        text = currentUser.email,
                        color = Color.White,
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Bold
                    )

                    Spacer(modifier = Modifier.height(3.dp))

                    Text(
                        text = "UID: ${currentUser.localId}",
                        color = Color(0xFF87829B),
                        fontSize = 11.sp,
                        fontFamily = androidx.compose.ui.text.font.FontFamily.Monospace
                    )

                    Spacer(modifier = Modifier.height(14.dp))

                    // Stats Card (matching PC)
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(12.dp))
                            .background(Color.White.copy(alpha = 0.06f))
                            .padding(vertical = 12.dp, horizontal = 16.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(
                                text = "Đã tải: $savedTracksCount bài  •  Bộ sưu tập: $collectionsCount",
                                color = Color(0xFFC8C3DC),
                                fontSize = 13.sp,
                                fontWeight = FontWeight.Medium
                            )
                            Spacer(modifier = Modifier.height(4.dp))
                            Text(
                                text = "Lịch sử nghe: $historyCount bài",
                                color = Color(0xFFC8C3DC),
                                fontSize = 13.sp,
                                fontWeight = FontWeight.Medium
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(10.dp))

                    // Sync Status message
                    Text(
                        text = syncStatusMessage ?: "Đồng bộ tự động đám mây đang bật",
                        color = NeonPink,
                        fontSize = 12.sp,
                        fontStyle = androidx.compose.ui.text.font.FontStyle.Italic
                    )

                    Spacer(modifier = Modifier.height(14.dp))

                    // Single Action Button: Đồng Bộ Ngay (Tự Động Bật)
                    Button(
                        onClick = onSyncNow,
                        enabled = !isSyncing,
                        colors = ButtonDefaults.buttonColors(containerColor = NeonPink),
                        shape = RoundedCornerShape(12.dp),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(44.dp)
                    ) {
                        if (isSyncing) {
                            CircularProgressIndicator(color = Color.White, strokeWidth = 2.dp, modifier = Modifier.size(18.dp))
                            Spacer(modifier = Modifier.width(8.dp))
                            Text("⟳  Đang Đồng Bộ...", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                        } else {
                            Text("⟳  Đồng Bộ Ngay (Tự Động Bật)", fontSize = 13.sp, fontWeight = FontWeight.Bold)
                        }
                    }

                    Spacer(modifier = Modifier.height(10.dp))

                    // Sign Out Button
                    OutlinedButton(
                        onClick = { showSignOutConfirm = true },
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(42.dp),
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.outlinedButtonColors(
                            contentColor = Color(0xFFFF7882)
                        )
                    ) {
                        Text("Đăng Xuất", color = Color(0xFFFF7882), fontSize = 13.sp, fontWeight = FontWeight.Bold)
                    }
                }
            } else {
                // ================= AUTH FORM (LOGIN / REGISTER) =================
                Text(
                    text = "Tài Khoản & Đồng Bộ",
                    color = Color.White,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = "Đăng nhập để đồng bộ toàn bộ danh sách phát, bộ sưu tập và tự động tải lại nhạc khi đổi máy.",
                    color = Color.White.copy(alpha = 0.6f),
                    fontSize = 12.sp,
                    lineHeight = 16.sp,
                    modifier = Modifier.padding(top = 2.dp)
                )

                Spacer(modifier = Modifier.height(14.dp))

                TabRow(
                    selectedTabIndex = selectedTab,
                    containerColor = Color.Transparent,
                    contentColor = NeonPink,
                    indicator = { tabPositions ->
                        TabRowDefaults.SecondaryIndicator(
                            modifier = Modifier.tabIndicatorOffset(tabPositions[selectedTab]),
                            color = NeonPink,
                            height = 2.5.dp
                        )
                    }
                ) {
                    Tab(
                        selected = selectedTab == 0,
                        onClick = {
                            selectedTab = 0
                            confirmPassword = ""
                            localError = null
                        },
                        text = {
                            Text(
                                "Đăng Nhập",
                                fontWeight = if (selectedTab == 0) FontWeight.Bold else FontWeight.Normal,
                                color = if (selectedTab == 0) Color.White else Color.White.copy(alpha = 0.5f)
                            )
                        }
                    )
                    Tab(
                        selected = selectedTab == 1,
                        onClick = {
                            selectedTab = 1
                            confirmPassword = ""
                            localError = null
                        },
                        text = {
                            Text(
                                "Đăng Ký Mới",
                                fontWeight = if (selectedTab == 1) FontWeight.Bold else FontWeight.Normal,
                                color = if (selectedTab == 1) Color.White else Color.White.copy(alpha = 0.5f)
                            )
                        }
                    )
                }

                Spacer(modifier = Modifier.height(14.dp))

                // Email Field
                OutlinedTextField(
                    value = email,
                    onValueChange = {
                        email = it
                        localError = null
                    },
                    label = { Text("Địa chỉ Email", fontSize = 12.sp) },
                    leadingIcon = {
                        Icon(imageVector = Icons.Default.Email, contentDescription = "Email", tint = Color.White.copy(alpha = 0.5f), modifier = Modifier.size(18.dp))
                    },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email, imeAction = ImeAction.Next),
                    singleLine = true,
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = NeonPink,
                        unfocusedBorderColor = Color.White.copy(alpha = 0.2f),
                        focusedLabelColor = NeonPink,
                        unfocusedLabelColor = Color.White.copy(alpha = 0.5f),
                        focusedTextColor = Color.White,
                        unfocusedTextColor = Color.White
                    ),
                    modifier = Modifier.fillMaxWidth()
                )

                Spacer(modifier = Modifier.height(10.dp))

                // Password Field
                OutlinedTextField(
                    value = password,
                    onValueChange = {
                        password = it
                        localError = null
                    },
                    label = { Text("Mật khẩu", fontSize = 12.sp) },
                    leadingIcon = {
                        Icon(imageVector = Icons.Default.Lock, contentDescription = "Password", tint = Color.White.copy(alpha = 0.5f), modifier = Modifier.size(18.dp))
                    },
                    trailingIcon = {
                        IconButton(onClick = { showPassword = !showPassword }) {
                            Icon(
                                imageVector = if (showPassword) Icons.Default.VisibilityOff else Icons.Default.Visibility,
                                contentDescription = "Toggle password",
                                tint = Color.White.copy(alpha = 0.5f),
                                modifier = Modifier.size(18.dp)
                            )
                        }
                    },
                    visualTransformation = if (showPassword) VisualTransformation.None else PasswordVisualTransformation(),
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = if (selectedTab == 0) ImeAction.Done else ImeAction.Next),
                    keyboardActions = KeyboardActions(onDone = {
                        if (selectedTab == 0) onSignIn(email, password)
                    }),
                    singleLine = true,
                    colors = OutlinedTextFieldDefaults.colors(
                        focusedBorderColor = NeonPink,
                        unfocusedBorderColor = Color.White.copy(alpha = 0.2f),
                        focusedLabelColor = NeonPink,
                        unfocusedLabelColor = Color.White.copy(alpha = 0.5f),
                        focusedTextColor = Color.White,
                        unfocusedTextColor = Color.White
                    ),
                    modifier = Modifier.fillMaxWidth()
                )

                // Confirm Password Field (Chỉ xuất hiện ở tab Đăng Ký Mới)
                if (selectedTab == 1) {
                    Spacer(modifier = Modifier.height(10.dp))
                    OutlinedTextField(
                        value = confirmPassword,
                        onValueChange = {
                            confirmPassword = it
                            localError = null
                        },
                        label = { Text("Nhập lại mật khẩu", fontSize = 12.sp) },
                        leadingIcon = {
                            Icon(imageVector = Icons.Default.Lock, contentDescription = "Confirm Password", tint = Color.White.copy(alpha = 0.5f), modifier = Modifier.size(18.dp))
                        },
                        trailingIcon = {
                            IconButton(onClick = { showConfirmPassword = !showConfirmPassword }) {
                                Icon(
                                    imageVector = if (showConfirmPassword) Icons.Default.VisibilityOff else Icons.Default.Visibility,
                                    contentDescription = "Toggle confirm password",
                                    tint = Color.White.copy(alpha = 0.5f),
                                    modifier = Modifier.size(18.dp)
                                )
                            }
                        },
                        visualTransformation = if (showConfirmPassword) VisualTransformation.None else PasswordVisualTransformation(),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Done),
                        keyboardActions = KeyboardActions(onDone = {
                            if (password != confirmPassword) {
                                localError = "Mật khẩu xác nhận không khớp."
                            } else if (password.length < 6) {
                                localError = "Mật khẩu phải có ít nhất 6 ký tự."
                            } else {
                                onSignUp(email, password)
                            }
                        }),
                        singleLine = true,
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedBorderColor = NeonPink,
                            unfocusedBorderColor = Color.White.copy(alpha = 0.2f),
                            focusedLabelColor = NeonPink,
                            unfocusedLabelColor = Color.White.copy(alpha = 0.5f),
                            focusedTextColor = Color.White,
                            unfocusedTextColor = Color.White
                        ),
                        modifier = Modifier.fillMaxWidth()
                    )
                }

                // Quên mật khẩu chỉ xuất hiện ở tab Đăng Nhập
                if (selectedTab == 0) {
                    Spacer(modifier = Modifier.height(4.dp))
                    Box(
                        modifier = Modifier.fillMaxWidth(),
                        contentAlignment = Alignment.CenterEnd
                    ) {
                        Text(
                            text = "Quên mật khẩu?",
                            color = NeonPink,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Medium,
                            textDecoration = TextDecoration.Underline,
                            modifier = Modifier
                                .clickable {
                                    if (email.isBlank()) {
                                        Toast.makeText(context, "Vui lòng nhập địa chỉ email vào ô trên trước.", Toast.LENGTH_SHORT).show()
                                    } else {
                                        onForgotPassword(email)
                                    }
                                }
                                .padding(vertical = 4.dp, horizontal = 2.dp)
                        )
                    }
                }

                val displayError = localError ?: syncStatusMessage
                if (!displayError.isNullOrBlank()) {
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = displayError,
                        color = Color(0xFFFF6B81),
                        fontSize = 12.sp,
                        lineHeight = 16.sp
                    )
                }

                Spacer(modifier = Modifier.height(16.dp))

                // Submit Button
                Button(
                    onClick = {
                        if (selectedTab == 0) {
                            onSignIn(email, password)
                        } else {
                            if (password != confirmPassword) {
                                localError = "Mật khẩu xác nhận không khớp."
                                Toast.makeText(context, "Mật khẩu xác nhận không khớp.", Toast.LENGTH_SHORT).show()
                                return@Button
                            }
                            if (password.length < 6) {
                                localError = "Mật khẩu phải có ít nhất 6 ký tự."
                                Toast.makeText(context, "Mật khẩu phải có ít nhất 6 ký tự.", Toast.LENGTH_SHORT).show()
                                return@Button
                            }
                            onSignUp(email, password)
                        }
                    },
                    enabled = !isSyncing && email.isNotBlank() && password.isNotBlank() && (selectedTab == 0 || confirmPassword.isNotBlank()),
                    colors = ButtonDefaults.buttonColors(containerColor = NeonPink),
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp)
                ) {
                    if (isSyncing) {
                        CircularProgressIndicator(color = Color.White, strokeWidth = 2.5.dp, modifier = Modifier.size(20.dp))
                    } else {
                        Text(
                            text = if (selectedTab == 0) "Đăng Nhập" else "Tạo Tài Khoản Mới",
                            fontSize = 14.5.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            }

            Spacer(modifier = Modifier.height(20.dp))
        }
    }
}
