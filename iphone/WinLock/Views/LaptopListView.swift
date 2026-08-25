import SwiftUI

struct LaptopListView: View {
    @ObservedObject var container: ServiceContainer
    @StateObject private var viewModel: LaptopListViewModel

    init(container: ServiceContainer) {
        self.container = container
        _viewModel = StateObject(wrappedValue: LaptopListViewModel(
            discovery: container.discovery,
            pairedStore: container.pairedLaptops,
            container: container))
    }

    var body: some View {
        NavigationStack {
            List {
                pairedSection
                discoveredSection
            }
            .navigationTitle("My Laptop")
            .navigationDestination(for: PairedLaptop.self) { laptop in
                LaptopDetailView(
                    laptop: laptop,
                    client: container.client(for: laptop),
                    log: container.logStore)
            }
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        viewModel.startDiscovery()
                    } label: {
                        Image(systemName: "arrow.clockwise")
                    }
                }
            }
            .task { viewModel.startDiscovery() }
        }
    }

    private var pairedSection: some View {
        Section("Paired") {
            if container.pairedLaptops.laptops.isEmpty {
                Text("No laptops paired yet. Discover your laptop below and tap Pair.")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            ForEach(container.pairedLaptops.laptops) { laptop in
                NavigationLink(value: laptop) {
                    LaptopRow(name: laptop.name, detail: "\(laptop.host):\(laptop.port)")
                }
            }
            .onDelete { indexSet in
                let deviceIds = indexSet.map { container.pairedLaptops.laptops[$0].deviceId }
                Task {
                    for deviceId in deviceIds {
                        await viewModel.unpair(deviceId: deviceId)
                    }
                }
            }
        }
    }

    private var discoveredSection: some View {
        Section("Discovered on network") {
            if viewModel.discovered.isEmpty {
                Text("Searching for WinLock laptops on the local network…")
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
            ForEach(viewModel.discovered) { laptop in
                HStack {
                    VStack(alignment: .leading) {
                        Text(laptop.name)
                        Text(laptop.host.isEmpty ? "Resolving…" : "\(laptop.host):\(laptop.port)")
                            .font(.caption)
                            .foregroundColor(.secondary)
                    }
                    Spacer()
                    if viewModel.isPaired(deviceId: laptop.deviceId) {
                        Label("Paired", systemImage: "checkmark.seal.fill")
                            .font(.caption)
                            .foregroundColor(.green)
                    } else {
                        NavigationLink("Pair") {
                            PairingView(host: laptop.host, port: laptop.port, container: container)
                        }
                    }
                }
            }
        }
    }
}

struct LaptopRow: View {
    let name: String
    let detail: String

    var body: some View {
        VStack(alignment: .leading) {
            Text(name).font(.headline)
            Text(detail).font(.caption).foregroundColor(.secondary)
        }
    }
}