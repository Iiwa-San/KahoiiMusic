import SwiftUI
#if canImport(UIKit)
import UIKit

public struct GradientPalette: Sendable {
    public let topColor: Color
    public let bottomColor: Color
    public let accentColor: Color

    public static let `default` = GradientPalette(
        topColor: Color(red: 0.12, green: 0.08, blue: 0.22),
        bottomColor: Color(red: 0.05, green: 0.04, blue: 0.09),
        accentColor: Color(red: 0.85, green: 0.35, blue: 0.85)
    )
}

public actor ColorExtractor {
    public static let shared = ColorExtractor()

    public func extractColors(from imageURL: URL) async -> GradientPalette {
        do {
            let (data, _) = try await URLSession.shared.data(from: imageURL)
            guard let uiImage = UIImage(data: data) else {
                return .default
            }
            return extractFromUIImage(uiImage)
        } catch {
            return .default
        }
    }

    private func extractFromUIImage(_ image: UIImage) -> GradientPalette {
        guard let cgImage = image.cgImage else { return .default }

        // Resize down to 16x16 to quickly sample dominant colors
        let width = 16
        let height = 16
        let bytesPerPixel = 4
        let bytesPerRow = bytesPerPixel * width
        let bitsPerComponent = 8
        var rawData = [UInt8](repeating: 0, count: width * height * bytesPerPixel)

        let colorSpace = CGColorSpaceCreateDeviceRGB()
        guard let context = CGContext(
            data: &rawData,
            width: width,
            height: height,
            bitsPerComponent: bitsPerComponent,
            bytesPerRow: bytesPerRow,
            space: colorSpace,
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue
        ) else {
            return .default
        }

        context.draw(cgImage, in: CGRect(x: 0, y: 0, width: width, height: height))

        var totalR: CGFloat = 0
        var totalG: CGFloat = 0
        var totalB: CGFloat = 0
        var count: CGFloat = 0

        var vibrantR: CGFloat = 0
        var vibrantG: CGFloat = 0
        var vibrantB: CGFloat = 0
        var maxSaturation: CGFloat = 0

        for i in stride(from: 0, to: rawData.count, by: bytesPerPixel) {
            let r = CGFloat(rawData[i]) / 255.0
            let g = CGFloat(rawData[i + 1]) / 255.0
            let b = CGFloat(rawData[i + 2]) / 255.0

            // Skip very dark or almost pure white pixels
            let maxVal = max(r, max(g, b))
            let minVal = min(r, min(g, b))
            let delta = maxVal - minVal

            totalR += r
            totalG += g
            totalB += b
            count += 1

            let sat = maxVal > 0 ? (delta / maxVal) : 0
            if sat > maxSaturation && maxVal > 0.3 && maxVal < 0.95 {
                maxSaturation = sat
                vibrantR = r
                vibrantG = g
                vibrantB = b
            }
        }

        guard count > 0 else { return .default }

        let avgR = totalR / count
        let avgG = totalG / count
        let avgB = totalB / count

        // Darken for background readability
        let top = Color(
            red: min(0.35, Double(vibrantR > 0 ? vibrantR * 0.45 : avgR * 0.4)),
            green: min(0.35, Double(vibrantG > 0 ? vibrantG * 0.45 : avgG * 0.4)),
            blue: min(0.45, Double(vibrantB > 0 ? vibrantB * 0.55 : avgB * 0.5))
        )

        let bottom = Color(
            red: Double(avgR * 0.12),
            green: Double(avgG * 0.12),
            blue: Double(avgB * 0.15)
        )

        let accent = Color(
            red: Double(vibrantR > 0 ? vibrantR : 0.8),
            green: Double(vibrantG > 0 ? vibrantG : 0.4),
            blue: Double(vibrantB > 0 ? vibrantB : 0.9)
        )

        return GradientPalette(topColor: top, bottomColor: bottom, accentColor: accent)
    }
}
#endif

