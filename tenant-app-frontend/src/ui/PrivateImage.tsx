import { useEffect, useState } from 'react';
import { Image, Platform, View, type ImageProps } from 'react-native';
import { absoluteUrl, authHeaders, checkedFetch } from '@/api/http';
import { AppText } from './primitives';

/** RN Web ignores source.headers: load protected images as authenticated blobs. */
export function PrivateImage({ uri, ...props }: Omit<ImageProps, 'source'> & { uri: string }) {
  const [local, setLocal] = useState<string>();
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    if (Platform.OS !== 'web') return;
    let active = true; let blobUrl: string | undefined;
    setLocal(undefined); setFailed(false);
    void checkedFetch(absoluteUrl(uri), { headers: authHeaders() }).then(r => r.blob()).then(blob => {
      if (!active) return;
      blobUrl = URL.createObjectURL(blob); setLocal(blobUrl);
    }).catch(() => { if (active) setFailed(true); });
    return () => { active = false; if (blobUrl) URL.revokeObjectURL(blobUrl); };
  }, [uri]);
  if (Platform.OS === 'web' && !local) return <View style={[props.style, { alignItems: 'center', justifyContent: 'center' }]}><AppText variant="small">{failed ? 'Фото недоступно' : 'Загрузка фото…'}</AppText></View>;
  return <Image {...props} source={Platform.OS === 'web' ? { uri: local! } : { uri: absoluteUrl(uri), headers: authHeaders() }} />;
}
