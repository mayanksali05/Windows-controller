import { CameraView, useCameraPermissions } from 'expo-camera';
import { useLocalSearchParams, router } from 'expo-router';
import React, { useRef, useState } from 'react';
import { Alert, Button, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useServices } from '../src/services/servicesContext';
import { FaceIdCancelledError, InvalidPairingPayloadError } from '../src/api/errors';
import type { DiscoveredLaptop } from '../src/types/protocol';

export default function PairScreen() {
  const params = useLocalSearchParams<{ deviceId?: string; host?: string; port?: string; name?: string }>();
  const services = useServices();

  const laptop: DiscoveredLaptop = {
    deviceId: params.deviceId ?? '',
    name: params.name ?? params.deviceId ?? 'Laptop',
    host: params.host ?? '',
    port: Number(params.port ?? 8765),
  };

  const [permission, requestPermission] = useCameraPermissions();
  const [mode, setMode] = useState<'scan' | 'paste'>('scan');
  const [payload, setPayload] = useState('');
  const [busy, setBusy] = useState(false);
  const handledRef = useRef(false);

  const complete = async (raw: string) => {
    if (handledRef.current || busy) {
      return;
    }
    handledRef.current = true;
    setBusy(true);
    try {
      const paired = await services.pairing.pair(laptop, raw, services.createPairingClient);
      router.replace(`/laptop/${paired.deviceId}`);
    } catch (error) {
      if (error instanceof FaceIdCancelledError) {
        handledRef.current = false;
        return;
      }
      handledRef.current = false;
      const message = error instanceof InvalidPairingPayloadError ? error.message : error instanceof Error ? error.message : 'Pairing failed';
      Alert.alert('Pairing failed', message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={styles.screen}>
      <View style={styles.tabs}>
        <Pressable style={[styles.tab, mode === 'scan' && styles.tabActive]} onPress={() => setMode('scan')}>
          <Text style={[styles.tabText, mode === 'scan' && styles.tabTextActive]}>Scan QR</Text>
        </Pressable>
        <Pressable style={[styles.tab, mode === 'paste' && styles.tabActive]} onPress={() => setMode('paste')}>
          <Text style={[styles.tabText, mode === 'paste' && styles.tabTextActive]}>Paste</Text>
        </Pressable>
      </View>

      <Text style={styles.title}>Scan the QR code shown on “{laptop.name}” (Windows tray → Pair new device).</Text>

      {mode === 'scan' ? (
        <>
          {!permission?.granted ? (
            <Pressable style={styles.primaryButton} onPress={() => void requestPermission()}>
              <Text style={styles.primaryButtonText}>Allow camera</Text>
            </Pressable>
          ) : (
            <CameraView
              style={styles.camera}
              onBarcodeScanned={({ data }) => {
                if (data) {
                  void complete(data);
                }
              }}
              barcodeScannerSettings={{ barcodeTypes: ['qr'] }}
            />
          )}
        </>
      ) : (
        <View style={styles.pasteArea}>
          <TextInput
            style={styles.input}
            value={payload}
            onChangeText={setPayload}
            placeholder="Paste the pairing payload JSON"
            placeholderTextColor="#556"
            multiline
            autoCapitalize="none"
            autoCorrect={false}
          />
          <Pressable style={styles.primaryButton} disabled={busy || !payload} onPress={() => void complete(payload)}>
            <Text style={styles.primaryButtonText}>{busy ? 'Pairing…' : 'Submit'}</Text>
          </Pressable>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#0b1220', padding: 16, gap: 14 },
  tabs: { flexDirection: 'row', gap: 10 },
  tab: { flex: 1, borderRadius: 8, paddingVertical: 8, alignItems: 'center', backgroundColor: '#1a2536' },
  tabActive: { backgroundColor: '#24344d' },
  tabText: { color: '#7a8aa0', fontWeight: '600' },
  tabTextActive: { color: '#5ac8fa' },
  title: { color: '#7a8aa0', fontSize: 13 },
  camera: { flex: 1, borderRadius: 12, overflow: 'hidden' },
  pasteArea: { gap: 12 },
  input: {
    backgroundColor: '#1a2536',
    color: '#e6f1ff',
    borderRadius: 10,
    padding: 12,
    minHeight: 160,
    textAlignVertical: 'top',
  },
  primaryButton: {
    backgroundColor: '#5ac8fa',
    borderRadius: 10,
    paddingVertical: 14,
    alignItems: 'center',
  },
  primaryButtonText: { color: '#0b1220', fontWeight: '800', fontSize: 15 },
});