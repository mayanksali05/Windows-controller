import Foundation

/// Drives the laptop detail screen: status polling and the lock action.
/// The lock action is Face-ID-gated (the API client runs Face ID before
/// signing the authentication challenge).
@MainActor
final class LaptopDetailViewModel: ObservableObject {
    @Published var status: LaptopStatus?
    @Published var settings: AppSettings?
    @Published var errorMessage: String?
    @Published var isLocking = false

    let laptop: PairedLaptop
    private let client: APIClient
    private let log: LogStore

    init(laptop: PairedLaptop, client: APIClient, log: LogStore) {
        self.laptop = laptop
        self.client = client
        self.log = log
    }

    func loadStatus() async {
        do {
            status = try await client.getStatus()
            errorMessage = nil
        } catch {
            errorMessage = error.localizedDescription
        }

        if settings == nil {
            settings = try? await client.getSettings()
        }
    }

    func lock() async {
        isLocking = true
        defer { isLocking = false }

        log.add(kind: "LOCK_REQUESTED", "Requesting lock of \(laptop.name)")
        do {
            try await client.lock()
            log.add(kind: "LOCK_SUCCESS", "\(laptop.name) locked")
        } catch {
            log.add(kind: "LOCK_FAILED", "Lock failed: \(error.localizedDescription)")
            errorMessage = error.localizedDescription
        }

        await loadStatus()
    }
}