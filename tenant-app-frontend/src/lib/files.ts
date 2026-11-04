import { Linking } from 'react-native';
import * as WebBrowser from 'expo-web-browser';
import * as ImagePicker from 'expo-image-picker';
import * as DocumentPicker from 'expo-document-picker';
import { absoluteUrl } from '@/api/http';
import type { LocalFile } from '@/api/types';

/** Просмотр файла во встроенном браузере. */
export async function viewFile(url: string) {
  await WebBrowser.openBrowserAsync(absoluteUrl(url));
}

/** Скачивание: открываем ссылку в системе, браузер сам предложит сохранить файл. */
export async function downloadFile(url: string) {
  await Linking.openURL(absoluteUrl(url));
}

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
    mediaTypes: ['images'],
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
