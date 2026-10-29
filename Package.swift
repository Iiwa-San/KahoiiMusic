// swift-tools-version: 5.9

import PackageDescription

let package = Package(
    name: "KahoiiMusic",
    platforms: [
        .iOS(.v16),
        .macOS(.v13),
    ],
    products: [
        .library(
            name: "KahoiiMusic",
            targets: ["KahoiiMusic"]
        ),
    ],
    dependencies: [],
    targets: [
        .target(
            name: "KahoiiMusic",
            dependencies: [],
            path: "Sources/KahoiiMusic",
            resources: [
                .process("Resources")
            ]
        ),
    ]
)

