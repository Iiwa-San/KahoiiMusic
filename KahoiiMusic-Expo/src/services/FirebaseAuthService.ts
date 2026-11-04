import AsyncStorage from '@react-native-async-storage/async-storage';
import { FirebaseUserInfo } from '../types/music';

const USER_STORAGE_KEY = '@kahoii_firebase_user';
const API_KEY = 'AIzaSyB4fUCZZN41WmnxGeH8jSdz1M5mi2LhLX8';

export class FirebaseAuthService {
  private static instance: FirebaseAuthService;
  public currentUser: FirebaseUserInfo | null = null;
  private listeners: Set<() => void> = new Set();

  public static getInstance(): FirebaseAuthService {
    if (!FirebaseAuthService.instance) {
      FirebaseAuthService.instance = new FirebaseAuthService();
    }
    return FirebaseAuthService.instance;
  }

  private constructor() {
    this.loadUser();
  }

  public get isLoggedIn(): boolean {
    return this.currentUser !== null;
  }

  private async loadUser() {
    try {
      const data = await AsyncStorage.getItem(USER_STORAGE_KEY);
      if (data) {
        this.currentUser = JSON.parse(data);
        this.notify();
      }
    } catch (e) {
      console.warn('Load user error:', e);
    }
  }

  private async saveUser(user: FirebaseUserInfo | null) {
    this.currentUser = user;
    if (user) {
      await AsyncStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user));
    } else {
      await AsyncStorage.removeItem(USER_STORAGE_KEY);
    }
    this.notify();
  }

  public async signUp(
    email: string,
    password: string
  ): Promise<{ success: boolean; user?: FirebaseUserInfo; message?: string }> {
    const trimmed = email.trim();
    if (!trimmed || password.length < 6) {
      return { success: false, message: 'Email không hợp lệ hoặc mật khẩu phải từ 6 ký tự.' };
    }

    try {
      const res = await fetch(
        `https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=${API_KEY}`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            email: trimmed,
            password,
            returnSecureToken: true,
          }),
        }
      );

      const json = await res.json();
      if (res.ok && json.localId && json.idToken) {
        const user: FirebaseUserInfo = {
          localId: json.localId,
          email: trimmed,
          idToken: json.idToken,
        };
        await this.saveUser(user);
        return { success: true, user };
      } else {
        const err = json?.error?.message || 'Đăng ký thất bại';
        if (err.includes('EMAIL_EXISTS')) {
          return { success: false, message: 'Email này đã được đăng ký tài khoản.' };
        }
        return { success: false, message: err };
      }
    } catch (e: any) {
      return { success: false, message: e.message || 'Lỗi kết nối máy chủ.' };
    }
  }

  public async signIn(
    email: string,
    password: string
  ): Promise<{ success: boolean; user?: FirebaseUserInfo; message?: string }> {
    const trimmed = email.trim();
    if (!trimmed || !password) {
      return { success: false, message: 'Vui lòng nhập đầy đủ email và mật khẩu.' };
    }

    try {
      const res = await fetch(
        `https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=${API_KEY}`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            email: trimmed,
            password,
            returnSecureToken: true,
          }),
        }
      );

      const json = await res.json();
      if (res.ok && json.localId && json.idToken) {
        const user: FirebaseUserInfo = {
          localId: json.localId,
          email: trimmed,
          idToken: json.idToken,
        };
        await this.saveUser(user);
        return { success: true, user };
      } else {
        const err = json?.error?.message || 'Đăng nhập không thành công';
        if (
          err.includes('EMAIL_NOT_FOUND') ||
          err.includes('INVALID_PASSWORD') ||
          err.includes('INVALID_LOGIN_CREDENTIALS')
        ) {
          return { success: false, message: 'Email hoặc mật khẩu không chính xác.' };
        }
        return { success: false, message: err };
      }
    } catch (e: any) {
      return { success: false, message: e.message || 'Lỗi kết nối máy chủ.' };
    }
  }

  public async sendPasswordReset(email: string): Promise<{ success: boolean; message: string }> {
    const trimmed = email.trim();
    if (!trimmed || !trimmed.includes('@')) {
      return { success: false, message: 'Vui lòng nhập địa chỉ email hợp lệ.' };
    }

    try {
      const res = await fetch(
        `https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key=${API_KEY}`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            requestType: 'PASSWORD_RESET',
            email: trimmed,
          }),
        }
      );

      const json = await res.json();
      if (res.ok) {
        return {
          success: true,
          message: 'Đã gửi email khôi phục mật khẩu. Vui lòng kiểm tra hộp thư (kể cả mục Spam) của bạn.',
        };
      } else {
        const err = json?.error?.message || 'Gửi email thất bại';
        if (err.includes('EMAIL_NOT_FOUND')) {
          return { success: false, message: 'Email này chưa được đăng ký tài khoản.' };
        }
        return { success: false, message: err };
      }
    } catch (e: any) {
      return { success: false, message: e.message || 'Lỗi kết nối máy chủ.' };
    }
  }

  public async signOut(): Promise<void> {
    await this.saveUser(null);
  }

  public subscribe(listener: () => void) {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private notify() {
    this.listeners.forEach((l) => l());
  }
}
