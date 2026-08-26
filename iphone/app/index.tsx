import { Link, router } from 'expo-router';
import React from 'react';
import { FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { useLaptops } from '../src/hooks/useLaptops';

export default function LaptopListScreen() {
  const { paired, discovered, refreshDiscovery, unpair } = useLaptops();

  interface ListItem {
    key: string;
    kind: 'paired' | 'discovered';
    name: string;
    detail: string;
    deviceId: string;
    host: string;
    port: number;
  }

  const items: ListItem[] = [
    ...paired.map((l) => ({
      key: `paired-${l.deviceId}`,
      kind: 'paired' as const,
      name: l.name,
      detail: `${l.host}:${l.port}`,
      deviceId: l.deviceId,
      host: l.host,
      port: l.port,
    })),
    ...discovered.map((l) => ({
      key: `disc-${l.deviceId}`,
      kind: 'discovered' as const,
      name: l.name,
      detail: `${l.host || 'resolving…'}:${l.port}`,
      deviceId: l.deviceId,
      host: l.host,
      port: l.port,
    })),
  ];

  return (
    <FlatList
      style={styles.screen}
      contentContainerStyle={styles.content}
      ListHeaderComponent={
        <View style={styles.toolbar}>
          <Pressable style={styles.toolbarButton} onPress={refreshDiscovery}>
            <Text style={styles.toolbarButtonText}>Rescan</Text>
          </Pressable>
          <Link href="/settings" asChild>
            <Pressable style={styles.toolbarButton}>
              <Text style={styles.toolbarButtonText}>Settings</Text>
            </Pressable>
          </Link>
          <Link href="/logs" asChild>
            <Pressable style={styles.toolbarButton}>
              <Text style={styles.toolbarButtonText}>Logs</Text>
            </Pressable>
          </Link>
        </View>
      }
      ListEmptyComponent={<Text style={styles.hint}>Searching for WinLock laptops on the local network…</Text>}
      data={items}
      keyExtractor={(item) => item.key}
      renderItem={({ item }) => {
        const pairedItem = paired.find((p) => p.deviceId === item.deviceId);
        return (
          <View style={styles.row}>
            <Pressable
              style={styles.rowMain}
              onPress={() => {
                if (pairedItem) {
                  router.push(`/laptop/${item.deviceId}`);
                }
              }}
            >
              <Text style={styles.rowTitle}>{item.name}</Text>
              <Text style={styles.rowDetail}>{item.detail}</Text>
              <Text style={styles.rowTag}>
                {pairedItem ? 'Paired' : item.kind === 'discovered' ? 'On network' : '—'}
              </Text>
            </Pressable>
            {pairedItem ? (
              <Pressable style={styles.rowAction} onPress={() => void unpair(item.deviceId)}>
                <Text style={styles.rowActionText}>Unpair</Text>
              </Pressable>
            ) : item.kind === 'discovered' ? (
              <Link
                href={{
                  pathname: '/pair',
                  params: { deviceId: item.deviceId, host: item.host, port: String(item.port), name: item.name },
                }}
                asChild
              >
                <Pressable style={styles.rowAction}>
                  <Text style={styles.rowActionText}>Pair</Text>
                </Pressable>
              </Link>
            ) : null}
          </View>
        );
      }}
    />
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#0b1220' },
  content: { padding: 16, gap: 12 },
  toolbar: { flexDirection: 'row', gap: 10, marginBottom: 8 },
  toolbarButton: {
    backgroundColor: '#1a2536',
    borderRadius: 8,
    paddingHorizontal: 14,
    paddingVertical: 8,
  },
  toolbarButtonText: { color: '#5ac8fa', fontWeight: '600' },
  hint: { color: '#7a8aa0', textAlign: 'center', marginTop: 32 },
  row: {
    backgroundColor: '#1a2536',
    borderRadius: 12,
    padding: 14,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  rowMain: { flex: 1 },
  rowTitle: { color: '#e6f1ff', fontSize: 16, fontWeight: '600' },
  rowDetail: { color: '#7a8aa0', fontSize: 13, marginTop: 2 },
  rowTag: { color: '#5ac8fa', fontSize: 12, marginTop: 4 },
  rowAction: { backgroundColor: '#0b1220', borderRadius: 8, paddingHorizontal: 12, paddingVertical: 8 },
  rowActionText: { color: '#5ac8fa', fontWeight: '600' },
});