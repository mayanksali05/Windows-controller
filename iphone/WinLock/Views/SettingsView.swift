import SwiftUI

struct SettingsView: View {
    @ObservedObject var container: ServiceContainer
    @State private var showAbout = false

    var body: some View {
        NavigationStack {
            Form {
                Section("Device") {
                    LabeledContent("Device ID", value: container.deviceId)
                    LabeledContent("Identity", value: "Ed25519 · Keychain")
                }

                Section("Paired laptops") {
                    if container.pairedLaptops.laptops.isEmpty {
                        Text("None paired").foregroundColor(.secondary)
                    }
                    ForEach(container.pairedLaptops.laptops) { laptop in
                        LabeledContent(laptop.name, value: "\(laptop.host):\(laptop.port)")
                    }
                }

                Section("Security") {
                    Label("Face ID protects privileged actions", systemImage: "faceid")
                    Label("Challenge-response authentication", systemImage: "key")
                    Label("TLS certificate pinned", systemImage: "lock.shield")
                    Label("Bluetooth proximity only (never access)", systemImage: "wave.3.right")
                }

                Section("About") {
                    Button("About WinLock") { showAbout = true }
                }
            }
            .navigationTitle("Settings")
            .sheet(isPresented: $showAbout) { AboutView() }
        }
    }
}

struct AboutView: View {
    var body: some View {
        VStack(spacing: 16) {
            Image(systemName: "lock.shield.fill")
                .font(.system(size: 64))
                .foregroundColor(.accentColor)
            Text("WinLock").font(.title)
            Text("Securely locks your Windows laptop from your iPhone.\nUnlock via a supported Windows mechanism is a documented extension point.")
                .font(.body)
                .multilineTextAlignment(.center)
                .foregroundColor(.secondary)
        }
        .padding()
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}