import Foundation

/// Laptop status snapshot from `GET /api/v1/status`.
struct LaptopStatus: Decodable {
    let isLocked: Bool?
    let batteryPercent: Int?
    let serviceVersion: String
    let environment: String
    let lockAvailable: Bool
    let proximity: String
    let security: String
}

/// An authorized (paired) device from `GET /api/v1/pair/devices`.
struct AuthorizedDevice: Decodable, Identifiable {
    let deviceId: String
    let name: String
    let pairedAt: String

    var id: String { deviceId }
}