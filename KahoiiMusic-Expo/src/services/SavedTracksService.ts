import { Paths, Directory, File } from 'expo-file-system';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { PlaylistItem } from '../types/music';
import { YouTubeResolver } from './YouTubeResolver';
import { ErrorLogService } from './ErrorLogService';

const SAVED_TRACKS_KEY = '@kahoii_saved_tracks';

export class SavedTracksService {
  private static instance: SavedTracksService;
  public savedTracks: PlaylistItem[] = [];
  public downloadingIds: Set<string> = new Set();
  private listeners: Set<() => void> = new Set();

  public static getInstance(): SavedTracksService {
    if (!SavedTracksService.instance) {
      SavedTracksService.instance = new SavedTracksService();
    }
    return SavedTracksService.instance;
  }

  private constructor() {
    this.ensureDirs();
    this.loadSavedTracks();
  }

  public get savedDir(): Directory {
    return new Directory(Paths.document, 'Saved');
  }

  public get audioDir(): Directory {
    return new Directory(this.savedDir, 'audio');
  }

  public get thumbDir(): Directory {
    return new Directory(this.savedDir, 'thumb');
  }

  private ensureDirs() {
    try {
      if (!this.savedDir.exists) {
        this.savedDir.create({ intermediates: true, idempotent: true });
      }
      if (!this.audioDir.exists) {
        this.audioDir.create({ intermediates: true, idempotent: true });
      }
      if (!this.thumbDir.exists) {
        this.thumbDir.create({ intermediates: true, idempotent: true });
      }
    } catch (e: any) {
      ErrorLogService.getInstance().log('Lỗi tạo thư mục lưu trữ', e.message, 'Lưu trữ');
    }
  }

  public async loadSavedTracks(): Promise<PlaylistItem[]> {
    try {
      const json = await AsyncStorage.getItem(SAVED_TRACKS_KEY);
      if (json) {
        this.savedTracks = JSON.parse(json);
        this.notify();
      }
    } catch (e: any) {
      console.warn('Load saved tracks error:', e);
    }
    return this.savedTracks;
  }

  private async persistSavedTracks() {
    try {
      await AsyncStorage.setItem(SAVED_TRACKS_KEY, JSON.stringify(this.savedTracks));
      this.notify();
    } catch (e: any) {
      console.warn('Persist saved tracks error:', e);
    }
  }

  public isSaved(id: string): boolean {
    return this.savedTracks.some((t) => t.id === id);
  }

  public isDownloading(id: string): boolean {
    return this.downloadingIds.has(id);
  }

  public getLocalAudioUri(id: string): string {
    const file = new File(this.audioDir, `${id}.m4a`);
    return file.uri;
  }

  public async saveTrackAsync(track: PlaylistItem): Promise<boolean> {
    if (this.isSaved(track.id)) return true;
    if (this.downloadingIds.has(track.id)) return false;

    this.downloadingIds.add(track.id);
    this.notify();

    try {
      this.ensureDirs();

      // Always resolve fresh stream URL for offline downloading to prevent 403 expired links
      const resolved = await YouTubeResolver.getInstance().resolveAudioStream(track.id);
      const streamUrl = resolved.streamUrl;

      const targetAudioFile = new File(this.audioDir, `${track.id}.m4a`);
      const targetThumbFile = new File(this.thumbDir, `${track.id}.jpg`);

      // Download audio file
      await File.downloadFileAsync(streamUrl, targetAudioFile, { idempotent: true });

      // Download thumbnail
      try {
        const thumbUrl =
          track.thumbnailUrl || `https://i.ytimg.com/vi/${track.id}/hqdefault.jpg`;
        await File.downloadFileAsync(thumbUrl, targetThumbFile, { idempotent: true });
      } catch {
        // Thumbnail failure is non-fatal
      }

      const savedItem: PlaylistItem = {
        ...track,
        localAudioUri: targetAudioFile.uri,
        addedAt: Date.now(),
      };

      this.savedTracks.unshift(savedItem);
      await this.persistSavedTracks();
      return true;
    } catch (e: any) {
      ErrorLogService.getInstance().log(
        'Lỗi tải bài hát offline',
        `${track.title}: ${e.message}`,
        'Tải về'
      );
      return false;
    } finally {
      this.downloadingIds.delete(track.id);
      this.notify();
    }
  }

  public async deleteTrack(id: string): Promise<void> {
    try {
      const audioFile = new File(this.audioDir, `${id}.m4a`);
      const thumbFile = new File(this.thumbDir, `${id}.jpg`);

      if (audioFile.exists) audioFile.delete();
      if (thumbFile.exists) thumbFile.delete();

      this.savedTracks = this.savedTracks.filter((t) => t.id !== id);
      await this.persistSavedTracks();
    } catch (e: any) {
      console.warn('Delete track error:', e);
    }
  }

  public async clearAllSaved(exceptId?: string): Promise<void> {
    const toDelete = this.savedTracks.filter((t) => t.id !== exceptId);
    for (const t of toDelete) {
      await this.deleteTrack(t.id);
    }
  }

  public async clearAll(): Promise<void> {
    for (const t of this.savedTracks) {
      await this.deleteTrack(t.id);
    }
  }

  public subscribe(listener: () => void) {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  private notify() {
    this.listeners.forEach((l) => l());
  }
}
