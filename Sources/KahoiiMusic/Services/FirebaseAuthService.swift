import Foundation
import Combine

public struct FirebaseUserInfo: Codable {
    public let localId: String
    public let email: String
    public let idToken: String

    public init(localId: String, email: String, idToken: String) {
        self.localId = localId
        self.email = email
        self.idToken = idToken
    }
}

public enum AuthError: LocalizedError {
    case custom(String)
    public var errorDescription: String? {
        switch self {
        case .custom(let msg): return msg
        }
    }
}

@MainActor
public class FirebaseAuthService: ObservableObject {
    public static let shared = FirebaseAuthService()

    @Published public var currentUser: FirebaseUserInfo?

    private let apiKey = "AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8"
    private let userDefaultsKey = "kahoii_firebase_user"

    private init() {
        loadUser()
    }

    public var isLoggedIn: Bool {
        currentUser != nil
    }

    private func loadUser() {
        if let data = UserDefaults.standard.data(forKey: userDefaultsKey),
           let user = try? JSONDecoder().decode(FirebaseUserInfo.self, from: data) {
            self.currentUser = user
        }
    }

    private func saveUser(_ user: FirebaseUserInfo?) {
        self.currentUser = user
        if let user = user, let data = try? JSONEncoder().encode(user) {
            UserDefaults.standard.set(data, forKey: userDefaultsKey)
        } else {
            UserDefaults.standard.removeObject(forKey: userDefaultsKey)
        }
    }

    public func signUp(email: String, password: String) async -> Result<FirebaseUserInfo, AuthError> {
        let trimmedEmail = email.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmedEmail.isEmpty || password.count < 6 {
            return .failure(.custom("Email không hợp lệ hoặc mật khẩu phải từ 6 ký tự trở lên."))
        }

        let endpoint = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=\(apiKey)"
        guard let url = URL(string: endpoint) else {
            return .failure(.custom("URL không hợp lệ."))
        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let body: [String: Any] = [
            "email": trimmedEmail,
            "password": password,
            "returnSecureToken": true
        ]
        request.httpBody = try? JSONSerialization.data(withJSONObject: body)

        do {
            let (data, response) = try await URLSession.shared.data(for: request)
            guard let httpResponse = response as? HTTPURLResponse else {
                return .failure(.custom("Lỗi kết nối máy chủ."))
            }

            if httpResponse.statusCode == 200 {
                if let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                   let localId = json["localId"] as? String,
                   let idToken = json["idToken"] as? String {
                    let user = FirebaseUserInfo(localId: localId, email: trimmedEmail, idToken: idToken)
                    self.saveUser(user)
                    return .success(user)
                }
            }

            // Error response
            if let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
               let errObj = json["error"] as? [String: Any],
               let message = errObj["message"] as? String {
                if message.contains("EMAIL_EXISTS") {
                    return .failure(.custom("Email này đã được đăng ký."))
                } else if message.contains("CONFIGURATION_NOT_FOUND") {
                    return .failure(.custom("Chưa bật Email/Password Auth trong Firebase Console."))
                }
                return .failure(.custom("Lỗi: \(message)"))
            }

            return .failure(.custom("Đăng ký thất bại (Mã lỗi \(httpResponse.statusCode))."))
        } catch {
            return .failure(.custom("Lỗi kết nối: \(error.localizedDescription)"))
        }
    }

    public func signIn(email: String, password: String) async -> Result<FirebaseUserInfo, AuthError> {
        let trimmedEmail = email.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmedEmail.isEmpty || password.isEmpty {
            return .failure(.custom("Vui lòng nhập email và mật khẩu."))
        }

        let endpoint = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=\(apiKey)"
        guard let url = URL(string: endpoint) else {
            return .failure(.custom("URL không hợp lệ."))
        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        let body: [String: Any] = [
            "email": trimmedEmail,
            "password": password,
            "returnSecureToken": true
        ]
        request.httpBody = try? JSONSerialization.data(withJSONObject: body)

        do {
            let (data, response) = try await URLSession.shared.data(for: request)
            guard let httpResponse = response as? HTTPURLResponse else {
                return .failure(.custom("Lỗi kết nối máy chủ."))
            }

            if httpResponse.statusCode == 200 {
                if let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                   let localId = json["localId"] as? String,
                   let idToken = json["idToken"] as? String {
                    let user = FirebaseUserInfo(localId: localId, email: trimmedEmail, idToken: idToken)
                    self.saveUser(user)
                    return .success(user)
                }
            }

            // Error response
            if let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
               let errObj = json["error"] as? [String: Any],
               let message = errObj["message"] as? String {
                if message.contains("EMAIL_NOT_FOUND") || message.contains("INVALID_LOGIN_CREDENTIALS") || message.contains("INVALID_PASSWORD") {
                    return .failure(.custom("Email hoặc mật khẩu không chính xác."))
                } else if message.contains("USER_DISABLED") {
                    return .failure(.custom("Tài khoản đã bị tạm khóa."))
                } else if message.contains("CONFIGURATION_NOT_FOUND") {
                    return .failure(.custom("Chưa bật Email/Password Auth trong Firebase Console."))
                }
                return .failure(.custom("Lỗi: \(message)"))
            }

            return .failure(.custom("Đăng nhập thất bại (Mã lỗi \(httpResponse.statusCode))."))
        } catch {
            return .failure(.custom("Lỗi kết nối: \(error.localizedDescription)"))
        }
    }

    public func signOut() {
        saveUser(nil)
    }
}
