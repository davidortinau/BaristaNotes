#if DEBUG
import Foundation
import AilohaAgent

@objc(BNAilohaBridge)
public final class NativeAgentBridge: NSObject {
    @objc(startWithCompletion:)
    public static func start(completion: @escaping (Int, NSError?) -> Void) {
        Task {
            do {
                // A fixed proof port prevents accidental selection of the adjacent MAUI app.
                let deviceIdentity = ProcessInfo.processInfo.environment["SIMULATOR_UDID"] ?? "physical-device"
                let configuration = AilohaAgentConfiguration(
                    port: 9243,
                    portFallbackCount: 0,
                    broker: BrokerConfiguration(
                        host: "127.0.0.1",
                        project: "BaristaNotes.Native.iOS",
                        sessionId: "baristanotes-native-ios-\(deviceIdentity)"
                    ),
                    enableNetworkCapture: false,
                    enableWebSocketStreams: false
                )
                try await Agent.shared.start(configuration)
                let status = await Agent.shared.status()
                guard status.running, let port = status.port else {
                    throw NSError(domain: "BaristaNotes.NativeAgent", code: 1,
                        userInfo: [NSLocalizedDescriptionKey: "Ailoha did not enter the listening state."])
                }
                await MainActor.run { completion(Int(port), nil) }
            } catch {
                let failure = error as NSError
                await MainActor.run { completion(0, failure) }
            }
        }
    }
}
#endif
