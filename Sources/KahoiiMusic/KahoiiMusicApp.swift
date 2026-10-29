import SwiftUI

@main
public struct KahoiiMusicApp: App {
    public init() {}

    public var body: some Scene {
        WindowGroup {
            MainPlayerView()
                .preferredColorScheme(.dark)
        }
    }
}

