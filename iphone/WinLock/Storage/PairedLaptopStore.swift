import Foundation

/// Persistent store of paired-laptop metadata (non-secret). Keys and pins live
/// in the Keychain.
@MainActor
final class PairedLaptopStore: ObservableObject {
    @Published private(set) var laptops: [PairedLaptop] = []

    private static let defaultsKey = "pairedLaptops"

    init() {
        load()
    }

    func add(_ laptop: PairedLaptop) {
        if let index = laptops.firstIndex(where: { $0.deviceId == laptop.deviceId }) {
            laptops[index] = laptop
        } else {
            laptops.append(laptop)
        }
        save()
    }

    func remove(deviceId: String) {
        laptops.removeAll { $0.deviceId == deviceId }
        save()
    }

    func laptop(deviceId: String) -> PairedLaptop? {
        laptops.first { $0.deviceId == deviceId }
    }

    private func load() {
        guard let data = UserDefaults.standard.data(forKey: Self.defaultsKey),
              let decoded = try? JSONDecoder().decode([PairedLaptop].self, from: data) else {
            return
        }
        laptops = decoded
    }

    private func save() {
        guard let data = try? JSONEncoder().encode(laptops) else { return }
        UserDefaults.standard.set(data, forKey: Self.defaultsKey)
    }
}

/// Local security event log shown in the app (does not replace the Windows
/// service's structured log).
@MainActor
final class LogStore: ObservableObject {
    @Published private(set) var entries: [LogEntry] = []

    func add(kind: String, _ message: String) {
        entries.insert(LogEntry(date: Date(), kind: kind, message: message), at: 0)
        if entries.count > 200 {
            entries.removeLast(entries.count - 200)
        }
    }
}

struct LogEntry: Identifiable {
    let id = UUID()
    let date: Date
    let kind: String
    let message: String
}