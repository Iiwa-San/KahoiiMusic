import SwiftUI
#if canImport(UIKit)
import UIKit
#endif

public struct ErrorLogsSheet: View {
    @ObservedObject var errorLog = ErrorLogService.shared
    @Environment(\.dismiss) private var dismiss

    public init() {}

    public var body: some View {
        NavigationView {
            ZStack {
                Color(white: 0.08).ignoresSafeArea()

                VStack(spacing: 0) {
                    if errorLog.logs.isEmpty {
                        VStack(spacing: 12) {
                            Spacer()
                            Image(systemName: "checkmark.shield.fill")
                                .font(.system(size: 48))
                                .foregroundColor(.green.opacity(0.8))
                            Text("Không có lỗi nào được ghi nhận")
                                .font(.system(size: 16, weight: .semibold))
                                .foregroundColor(.white.opacity(0.8))
                            Text("Hệ thống và các chức năng đang hoạt động bình thường.")
                                .font(.system(size: 13))
                                .foregroundColor(.white.opacity(0.5))
                            Spacer()
                        }
                        .padding()
                    } else {
                        List {
                            ForEach(errorLog.logs) { item in
                                VStack(alignment: .leading, spacing: 6) {
                                    HStack {
                                        Text(item.category.uppercased())
                                            .font(.system(size: 10, weight: .bold))
                                            .padding(.horizontal, 6)
                                            .padding(.vertical, 2)
                                            .background(Color.red.opacity(0.2))
                                            .foregroundColor(.red)
                                            .clipShape(Capsule())

                                        Text(item.title)
                                            .font(.system(size: 14, weight: .bold))
                                            .foregroundColor(.white)

                                        Spacer()

                                        Text(item.formattedTime)
                                            .font(.system(size: 11, design: .monospaced))
                                            .foregroundColor(.white.opacity(0.4))
                                    }

                                    Text(item.message)
                                        .font(.system(size: 12))
                                        .foregroundColor(.white.opacity(0.8))
                                        .textSelection(.enabled)
                                }
                                .padding(.vertical, 6)
                                .listRowBackground(Color.white.opacity(0.04))
                                .listRowSeparatorTint(Color.white.opacity(0.08))
                            }
                        }
                        .listStyle(.plain)
                        .scrollContentBackground(.hidden)
                    }
                }
            }
            .navigationTitle("Nhật ký lỗi hệ thống")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    if !errorLog.logs.isEmpty {
                        Button("Xóa nhật ký") {
                            errorLog.clearLogs()
                        }
                        .font(.system(size: 14))
                        .foregroundColor(.red.opacity(0.8))
                    }
                }

                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Xong") {
                        dismiss()
                    }
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundColor(.white)
                }
            }
        }
    }
}
