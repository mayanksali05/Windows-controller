import Foundation
import Network

/// A laptop discovered on the local network via Bonjour/mDNS.
struct DiscoveredLaptop: Identifiable, Hashable {
    let id: String
    let name: String
    let host: String
    let port: Int
    let deviceId: String
}

/// Discovers WinLock laptops via Bonjour (`_mywinlock._tcp`). Discovery only
/// tells us a device exists; pairing establishes trust.
@MainActor
final class BonjourDiscovery: ObservableObject {
    @Published private(set) var laptops: [DiscoveredLaptop] = []

    private var browser: NWBrowser?

    func start() {
        guard browser == nil else { return }
        let browser = NWBrowser(for: .bonjour(type: "_mywinlock._tcp", domain: nil), using: .tcp)
        browser.browseResultsChangedHandler = { [weak self] results, _ in
            Task { @MainActor in
                self?.handle(results)
            }
        }
        browser.start(queue: .main)
        self.browser = browser
    }

    func stop() {
        browser?.cancel()
        browser = nil
        laptops.removeAll()
    }

    private func handle(_ results: Set<NWBrowser.Result>) {
        var found: [DiscoveredLaptop] = []
        for result in results {
            guard case .service(let name, _, _) = result.endpoint else { continue }
            let deviceId = result.metadata?.bonjourTxtRecord?["device_id"] ?? ""
            let laptop = DiscoveredLaptop(
                id: "\(name)-\(deviceId)",
                name: name,
                host: "",
                port: 0,
                deviceId: deviceId)
            found.append(laptop)

            resolve(result.endpoint) { [weak self] host, port in
                Task { @MainActor in
                    self?.updateResolved(deviceId: deviceId, host: host, port: port)
                }
            }
        }
        laptops = found
    }

    private func updateResolved(deviceId: String, host: String, port: Int) {
        guard let index = laptops.firstIndex(where: { $0.deviceId == deviceId }) else { return }
        let existing = laptops[index]
        laptops[index] = DiscoveredLaptop(
            id: existing.id,
            name: existing.name,
            host: host,
            port: port,
            deviceId: deviceId)
    }

    /// Best-effort resolution of a Bonjour service endpoint to a host:port.
    private func resolve(_ endpoint: NWEndpoint, completion: @escaping (String, Int) -> Void) {
        let connection = NWConnection(to: endpoint, using: .tcp)
        connection.stateUpdateHandler = { state in
            switch state {
            case .ready:
                if let remote = connection.currentPath?.remoteEndpoint {
                    if case .hostPort(let host, let port) = remote {
                        completion(host.debugDescription, Int(port.rawValue))
                    }
                }
                connection.cancel()
            case .failed:
                connection.cancel()
            default:
                break
            }
        }
        connection.start(queue: .global(qos: .utility))
    }
}