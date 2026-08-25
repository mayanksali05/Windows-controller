import SwiftUI

struct PairingView: View {
    @StateObject private var viewModel: PairingViewModel
    let host: String
    let port: Int
    @State private var showManualEntry = false
    @State private var manualPayload = ""

    init(host: String, port: Int, container: ServiceContainer) {
        self.host = host
        self.port = port
        _viewModel = StateObject(wrappedValue: PairingViewModel(container: container))
    }

    var body: some View {
        VStack(spacing: 16) {
            switch viewModel.stage {
            case .scanning:
                QRScannerView { payload in
                    Task { await viewModel.processPayload(payload, host: host, port: port) }
                }
                .frame(height: 300)
                .clipShape(RoundedRectangle(cornerRadius: 16))

                Text("Scan the QR code shown on the Windows laptop (tray → Pair new device).")
                    .font(.subheadline)
                    .foregroundColor(.secondary)
                    .multilineTextAlignment(.center)

                Button("Enter payload manually") { showManualEntry.toggle() }

                if showManualEntry {
                    TextField("Paste pairing payload", text: $manualPayload, axis: .vertical)
                        .textFieldStyle(.roundedBorder)
                    Button("Submit") {
                        Task { await viewModel.processPayload(manualPayload, host: host, port: port) }
                    }
                }

            case .verifying:
                ProgressView("Verifying laptop identity…")
            case .confirming:
                ProgressView("Confirming pairing…")
            case .done:
                Label("Paired successfully", systemImage: "checkmark.seal.fill")
                    .foregroundColor(.green)
            }

            if let error = viewModel.errorMessage {
                Text(error)
                    .font(.caption)
                    .foregroundColor(.red)
                    .multilineTextAlignment(.center)
            }
        }
        .padding()
        .navigationTitle("Pair laptop")
    }
}