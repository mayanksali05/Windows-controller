import CryptoKit
import ExpoModulesCore
import Foundation
import Network

/// Errors surfaced to JS with stable `code` values.
class CertificatePinningException: Exception {
  override var code: String { "ERR_CERTIFICATE" }
  override var reason: String { "TLS certificate did not match the pinned certificate" }
}

class InvalidRequestException: Exception {
  override var code: String { "ERR_INVALID_REQUEST" }
  override var reason: String { "The request was malformed" }
}

class TransportException: Exception {
  override var code: String { "ERR_TRANSPORT" }
  override var reason: String { "The request could not be completed" }
}

/// Validates the TLS server certificate against an exact pin (base64url of the
/// SHA-256 of the leaf DER). Never accepts arbitrary certificates. In
/// production mode the OS chain must also validate.
private final class PinnedDelegate: NSObject, URLSessionDelegate {
  private let expectedPin: String
  private let productionMode: Bool

  init(expectedPin: String, productionMode: Bool) {
    self.expectedPin = expectedPin
    self.productionMode = productionMode
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
    let actualPin = base64url(Data(digest))

    guard actualPin == expectedPin else {
      completionHandler(.cancelAuthenticationChallenge, nil)
      return
    }

    if productionMode {
      var error: CFError?
      guard SecTrustEvaluateWithError(trust, &error) else {
        completionHandler(.cancelAuthenticationChallenge, nil)
        return
      }
    }

    completionHandler(.useCredential, URLCredential(trust: trust))
  }

  private func base64url(_ data: Data) -> String {
    data.base64EncodedString()
      .replacingOccurrences(of: "=", with: "")
      .replacingOccurrences(of: "+", with: "-")
      .replacingOccurrences(of: "/", with: "_")
  }
}

public class WinlockNetworkingModule: Module {
  private var browser: NWBrowser?
  private var resolvers: [String: NWConnection] = [:]
  private let discoveryQueue = DispatchQueue(label: "com.winlock.discovery")

  public func definition() -> ModuleDefinition {
    Name("WinlockNetworking")

    AsyncFunction("pinnedRequest") { (options: [String: Any]) throws -> [String: Any] in
      try await self.pinnedRequest(options: options)
    }

    Function("startDiscovery") {
      self.startDiscovery()
    }

    Function("stopDiscovery") {
      self.stopDiscovery()
    }

    Events("onLaptopDiscovered")
  }

  // MARK: - Pinned HTTPS

  private func pinnedRequest(options: [String: Any]) async throws -> [String: Any] {
    guard let urlString = options["url"] as? String, let url = URL(string: urlString) else {
      throw InvalidRequestException()
    }
    let method = (options["method"] as? String) ?? "GET"
    let headers = options["headers"] as? [String: String] ?? [:]
    let body = options["body"] as? String
    guard let pin = options["pin"] as? String, !pin.isEmpty else {
      throw CertificatePinningException()
    }
    let mode = (options["mode"] as? String) ?? "development"

    var request = URLRequest(url: url)
    request.httpMethod = method
    request.timeoutInterval = 10
    for (key, value) in headers {
      request.setValue(value, forHTTPHeaderField: key)
    }
    if let body {
      request.httpBody = Data(body.utf8)
      request.setValue("application/json", forHTTPHeaderField: "Content-Type")
    }

    let delegate = PinnedDelegate(expectedPin: pin, productionMode: mode == "production")
    let session = URLSession(configuration: .ephemeral, delegate: delegate, delegateQueue: nil)
    defer { session.finishTasksAndInvalidate() }

    do {
      let (data, response) = try await session.data(for: request)
      guard let http = response as? HTTPURLResponse else {
        throw TransportException()
      }
      let bodyString = String(data: data, encoding: .utf8) ?? ""
      return ["status": http.statusCode, "body": bodyString]
    } catch is CertificatePinningException {
      throw CertificatePinningException()
    } catch let error as URLError where error.code == .cancelled {
      throw CertificatePinningException()
    } catch {
      throw TransportException()
    }
  }

  // MARK: - Bonjour discovery

  private func startDiscovery() {
    guard browser == nil else { return }
    let browser = NWBrowser(for: .bonjour(type: "_mywinlock._tcp", domain: nil), using: .tcp)
    browser.browseResultsChangedHandler = { [weak self] results, _ in
      self?.handle(results)
    }
    browser.start(queue: discoveryQueue)
    self.browser = browser
  }

  private func stopDiscovery() {
    browser?.cancel()
    browser = nil
    resolvers.values.forEach { $0.cancel() }
    resolvers.removeAll()
  }

  private func handle(_ results: Set<NWBrowser.Result>) {
    for result in results {
      guard case .service(let name, _, _) = result.endpoint else { continue }
      let deviceId = result.metadata?.bonjourTxtRecord?["device_id"] ?? ""
      resolve(result.endpoint, name: name, deviceId: deviceId)
    }
  }

  private func resolve(_ endpoint: NWEndpoint, name: String, deviceId: String) {
    let key = "\(name)-\(deviceId)"
    guard resolvers[key] == nil else { return }

    let connection = NWConnection(to: endpoint, using: .tcp)
    resolvers[key] = connection
    connection.stateUpdateHandler = { [weak self] state in
      switch state {
      case .ready:
        if let remote = connection.currentPath?.remoteEndpoint {
          if case .hostPort(let host, let port) = remote {
            self?.emitLaptop(name: name, deviceId: deviceId, host: host.debugDescription, port: Int(port.rawValue))
          }
        }
        connection.cancel()
        self?.resolvers[key] = nil
      case .failed:
        connection.cancel()
        self?.resolvers[key] = nil
      default:
        break
      }
    }
    connection.start(queue: discoveryQueue)
  }

  private func emitLaptop(name: String, deviceId: String, host: String, port: Int) {
    DispatchQueue.main.async {
      self.sendEvent("onLaptopDiscovered", [
        "name": name,
        "deviceId": deviceId,
        "host": host,
        "port": port,
      ])
    }
  }
}