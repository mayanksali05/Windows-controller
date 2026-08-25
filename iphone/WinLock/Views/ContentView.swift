import SwiftUI

struct ContentView: View {
    @ObservedObject var container: ServiceContainer

    var body: some View {
        TabView {
            LaptopListView(container: container)
                .tabItem { Label("Laptops", systemImage: "laptopcomputer") }

            SettingsView(container: container)
                .tabItem { Label("Settings", systemImage: "gearshape") }

            SecurityLogView(store: container.logStore)
                .tabItem { Label("Logs", systemImage: "list.bullet.shield") }
        }
        .task { container.updateAdvertising() }
    }
}