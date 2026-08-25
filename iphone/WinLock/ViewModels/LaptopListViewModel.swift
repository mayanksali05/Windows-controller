import Combine
import Foundation

/// Drives the laptop list: starts discovery and manages unpairing.
@MainActor
final class LaptopListViewModel: ObservableObject {
    @Published var discovered: [DiscoveredLaptop] = []
    @Published var errorMessage: String?
    @Published var isDiscovering = false

    private let discovery: BonjourDiscovery
    private let pairedStore: PairedLaptopStore
    private let container: ServiceContainer
    private var cancellables: Set<AnyCancellable> = []

    init(discovery: BonjourDiscovery, pairedStore: PairedLaptopStore, container: ServiceContainer) {
        self.discovery = discovery
        self.pairedStore = pairedStore
        self.container = container

        discovery.$laptops
            .receive(on: RunLoop.main)
            .assign(to: &$discovered)
    }

    func startDiscovery() {
        guard !isDiscovering else { return }
        isDiscovering = true
        discovery.start()
    }

    func isPaired(deviceId: String) -> Bool {
        pairedStore.laptop(deviceId: deviceId) != nil
    }

    func unpair(deviceId: String) async {
        guard let laptop = pairedStore.laptop(deviceId: deviceId) else { return }
        do {
            try await container.client(for: laptop).unpair(deviceId: deviceId)
            pairedStore.remove(deviceId: deviceId)
        } catch {
            errorMessage = error.localizedDescription
        }
    }
}