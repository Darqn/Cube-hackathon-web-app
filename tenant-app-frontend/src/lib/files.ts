import { Platform } from 'react-native';
import * as FileSystem from 'expo-file-system';
import * as Sharing from 'expo-sharing';
import * as WebBrowser from 'expo-web-browser';
import * as ImagePicker from 'expo-image-picker';
import * as DocumentPicker from 'expo-document-picker';
import { absoluteUrl, authHeaders, checkedFetch } from '@/api/http';
import type { LocalFile } from '@/api/types';

/** Private files are fetched with Bearer authorization; tokens never enter URLs. */
export async function downloadFile(url: string) {
  const target = absoluteUrl(url);
  if (Platform.OS === 'web') {
    const response = await checkedFetch(target, { headers: authHeaders() });
    const objectUrl = URL.createObjectURL(await response.blob());
    const a = document.createElement('a'); a.href = objectUrl;
    const disposition = response.headers.get('content-disposition') ?? '';
    const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
    a.download = encoded ? decodeURIComponent(encoded) : /filename="?([^";]+)/i.exec(disposition)?.[1] ?? 'document';
    a.click(); setTimeout(() => URL.revokeObjectURL(objectUrl), 60000);
  } else {
    const path = FileSystem.cacheDirectory + `cube-${Date.now()}`;
    try {
      const result = await FileSystem.downloadAsync(target, path, { headers: authHeaders() });
      if (result.status !== 200) throw new Error('Файл недоступен или сессия истекла.');
      if (await Sharing.isAvailableAsync()) await Sharing.shareAsync(path);
      else throw new Error('Просмотр файлов на этом устройстве недоступен.');
    } finally { await FileSystem.deleteAsync(path, { idempotent: true }); }
  }
}
export const viewFile = downloadFile;

export async function takePhoto(): Promise<LocalFile | null> {
  const perm = await ImagePicker.requestCameraPermissionsAsync();
  if (!perm.granted) throw new Error('Разрешите доступ к камере в настройках телефона.');
  const res = await ImagePicker.launchCameraAsync({ quality: 0.7 });
  if (res.canceled) return null;
  const a = res.assets[0];
  return { uri: a.uri, name: a.fileName ?? `photo-${Date.now()}.jpg`, mimeType: a.mimeType ?? 'image/jpeg' };
}

export async function pickImages(multiple = true): Promise<LocalFile[]> {
  const res = await ImagePicker.launchImageLibraryAsync({
    mediaTypes: ImagePicker.MediaTypeOptions.Images,
    allowsMultipleSelection: multiple,
    quality: 0.7,
  });
  if (res.canceled) return [];
  return res.assets.map((a, i) => ({
    uri: a.uri,
    name: a.fileName ?? `image-${Date.now()}-${i}.jpg`,
    mimeType: a.mimeType ?? 'image/jpeg',
  }));
}

export async function pickDocuments(multiple = true): Promise<LocalFile[]> {
  const res = await DocumentPicker.getDocumentAsync({ multiple, copyToCacheDirectory: true });
  if (res.canceled) return [];
  return res.assets.map((a) => ({ uri: a.uri, name: a.name, mimeType: a.mimeType ?? undefined }));
}
