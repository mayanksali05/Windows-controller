import CryptoKit
import Foundation

/// Deterministic BLE service UUID for a device (RFC 4122 v5). The iPhone
/// advertises its own derived UUID; the Windows scanner derives the same value
/// for each paired device so a phone can be identified by its advertised
/// service UUID without a connection. Mirrors `WinLock.Service.Bluetooth.ProximityUuid`
/// byte-for-byte (both format the final bytes in RFC 4122 network order).
enum ProximityUuid {
    static let namespaceUUID = UUID(uuidString: "9B2F6D21-8E4C-4E2A-9F6A-9D4E3B2C1A00")!

    static func serviceUUID(for deviceId: String) -> UUID {
        var data = Data()
        data.append(namespaceBytesInNetworkOrder)
        data.append(Data(deviceId.utf8))

        let digest = Insecure.SHA1.hash(data: data) // 20 bytes
        var bytes = [UInt8](digest.prefix(16))
        bytes[6] = (bytes[6] & 0x0F) | 0x50 // version 5
        bytes[8] = (bytes[8] & 0x3F) | 0x80 // RFC 4122 variant

        return uuidFromNetworkBytes(bytes)
    }

    /// The namespace UUID parsed from its canonical string, producing the RFC
    /// 4122 network-order bytes (identical to the .NET derivation).
    private static var namespaceBytesInNetworkOrder: [UInt8] {
        var bytes = [UInt8]()
        let hex = namespaceUUID.uuidString.replacingOccurrences(of: "-", with: "")
        var index = hex.startIndex
        while index < hex.endIndex {
            let end = hex.index(index, offsetBy: 2)
            bytes.append(UInt8(hex[index..<end], radix: 16)!)
            index = end
        }
        return bytes
    }

    private static func uuidFromNetworkBytes(_ bytes: [UInt8]) -> UUID {
        let hex = bytes.map { String(format: "%02x", $0) }.joined()
        let formatted =
            "\(hex.prefix(8))-\(hex.dropFirst(8).prefix(4))-\(hex.dropFirst(12).prefix(4))-" +
            "\(hex.dropFirst(16).prefix(4))-\(hex.dropFirst(20))"
        return UUID(uuidString: String(formatted))!
    }
}