import CryptoKit
import SwiftUI

@main
struct WinLockApp: App {
    @StateObject private var container = ServiceContainer()

    var body: some Scene {
        WindowGroup {
            ContentView(container: container)
        }
    }
}