import Foundation
import LocalAuthentication

/// Gate for privileged iPhone actions using Face ID / biometrics.
enum FaceIDAuthenticator {
    enum FaceIDError: Error {
        case unavailable
        case rejected
    }

    static func evaluate(reason: String) async throws {
        let context = LAContext()
        var error: NSError?
        guard context.canEvaluatePolicy(.deviceOwnerAuthenticationWithBiometrics, error: &error) else {
            throw FaceIDError.unavailable
        }

        let success = try await context.evaluatePolicy(
            .deviceOwnerAuthenticationWithBiometrics,
            localizedReason: reason)
        guard success else {
            throw FaceIDError.rejected
        }
    }
}