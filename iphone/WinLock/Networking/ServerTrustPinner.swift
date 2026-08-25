import CryptoKit
import Foundation
import Security

/// Certificate validation mode. The pin is always required (it is the trust
/// anchor); production additionally requires the OS chain to validate.
enum TrustMode {
    case development
    case production
}

/// Validates the TLS server certificate against an exact pin: the base64url
/// SHA-256 of the leaf certificate's DER encoding, delivered out of band in the
/// pairing QR payload. Never accepts arbitrary certificates.
final class ServerTrustPinner: NSObject, URLSessionDelegate {
    private let expectedPin: String?
    private let mode: TrustMode

    init(expectedPin: String?, mode: TrustMode) {
        self.expectedPin = expectedPin
        self.mode = mode
    }

    func urlSession(
        _ session: URLSession,
        didReceive challenge: URLAuthenticationChallenge,
        completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void
    ) {
        guard challenge.protectionSpace.authenticationMethod == NSURLAuthenticationMethodServerTrust,
              let trust = challenge.protectionSpace.serverTrust,
              let chain = SecTrustCopyCertificateChain(trust) as? [SecCertificate],
              let leaf = chain.first else {
            completionHandler(.cancelAuthenticationChallenge, nil)
            return
        }

        let leafData = SecCertificateCopyData(leaf) as Data
        let digest = SHA256.hash(data: leafData)
        let actualPin = Base64URL.encode(Data(digest))

        guard let expectedPin, actualPin == expectedPin else {
            completionHandler(.cancelAuthenticationChallenge, nil)
            return
        }

        if mode == .production {
            var error: CFError?
            guard SecTrustEvaluateWithError(trust, &error) else {
                completionHandler(.cancelAuthenticationChallenge, nil)
                return
            }
        }

        completionHandler(.useCredential, URLCredential(trust: trust))
    }
}