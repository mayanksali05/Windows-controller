import SwiftUI

/// Main laptop screen (mirrors the project's UI sketch).
struct LaptopDetailView: View {
    @StateObject private var viewModel: LaptopDetailViewModel

    init(laptop: PairedLaptop, client: APIClient, log: LogStore) {
        _viewModel = StateObject(wrappedValue: LaptopDetailViewModel(laptop: laptop, client: client, log: log))
    }

    var body: some View {
        ScrollView {
            VStack(spacing: 24) {
                statusHeader
                lockButton
                detailGrid
            }
            .padding()
        }
        .navigationTitle(viewModel.laptop.name)
        .navigationBarTitleDisplayMode(.inline)
        .task { await viewModel.loadStatus() }
    }

    private var statusHeader: some View {
        HStack {
            Circle()
                .fill(viewModel.status?.isLocked == false ? Color.green : Color.orange)
                .frame(width: 12, height: 12)
            Text(viewModel.status?.isLocked == false ? "Connected" : "Locked")
                .font(.title3)
            Spacer()
        }
    }

    private var lockButton: some View {
        Button {
            Task { await viewModel.lock() }
        } label: {
            Label("LOCK", systemImage: "lock.fill")
                .font(.headline)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 8)
        }
        .buttonStyle(.borderedProminent)
        .tint(.red)
        .disabled(viewModel.isLocking)
    }

    private var detailGrid: some View {
        Grid(horizontalSpacing: 24, verticalSpacing: 12) {
            GridRow {
                detail("Battery", value: batteryText)
                detail("Status", value: statusText)
            }
            GridRow {
                detail("Proximity", value: viewModel.status?.proximity ?? "—")
                detail("Security", value: viewModel.status?.security ?? "—")
            }
            GridRow {
                detail("Version", value: viewModel.status?.serviceVersion ?? "—")
                detail("Environment", value: viewModel.status?.environment ?? "—")
            }
        }
        .frame(maxWidth: .infinity)
    }

    private func detail(_ title: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title).font(.caption).foregroundColor(.secondary)
            Text(value).font(.body).fontWeight(.medium)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var batteryText: String {
        guard let percent = viewModel.status?.batteryPercent else { return "n/a" }
        return "\(percent)%"
    }

    private var statusText: String {
        guard let locked = viewModel.status?.isLocked else { return "Unknown" }
        return locked ? "Locked" : "Unlocked"
    }
}