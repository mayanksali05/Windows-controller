import SwiftUI

struct SecurityLogView: View {
    @ObservedObject var store: LogStore

    var body: some View {
        NavigationStack {
            List(store.entries) { entry in
                VStack(alignment: .leading, spacing: 4) {
                    Text(entry.message)
                        .font(.subheadline)
                    Text("\(entry.date, format: .dateTime.hour().minute().second()) · \(entry.kind)")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
            }
            .navigationTitle("Security log")
            .overlay {
                if store.entries.isEmpty {
                    ContentUnavailableView(
                        "No events",
                        systemImage: "shield",
                        description: Text("Privileged actions and authentication events will appear here."))
                }
            }
        }
    }
}