import { useLocalSearchParams } from 'expo-router';
import React, { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useLaptopDetail } from '../../src/hooks/useLaptopDetail';
import { useProximity } from '../../src/hooks/useProximity';
import { useServices } from '../../src/services/servicesContext';
import { findLaptop } from '../../src/storage/laptopStore';
import type { PairedLaptop } from '../../src/types/protocol';

export default function LaptopDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const services = useServices();
  const [laptop, setLaptop] = useState<PairedLaptop | null>(null);
  const [client, setClient] = useState<Awaited<ReturnType<typeof services.createClient>> | null>(null);

  useEffect(() => {
    let active = true;
    void (async () => {
      if (!id) {
        return;
      }
      const found = await findLaptop(id);
      if (!active || !found) {
        return;
      }
      setLaptop(found);
      try {
        const created = await services.createClient(found);
        if (active) {
          setClient(created);
        }
      } catch {
        // pin missing; keep laptop for display
      }
    })();
    return () => {
      active = false;
    };
  }, [id, services]);

  const detail = useLaptopDetail(client);
  const proximity = useProximity(client);

  if (!laptop) {
    return (
      <View style={styles.center}>
        <ActivityIndicator />
      </View>
    );
  }

  const status = detail.status;

  return (
    <ScrollView style={styles.screen} contentContainerStyle={styles.content}>
      <View style={styles.headerRow}>
        <View style={[styles.dot, status?.isLocked === false ? styles.dotConnected : styles.dotLocked]} />
        <Text style={styles.headerText}>{status?.isLocked === false ? 'Connected' : 'Locked'}</Text>
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>{laptop.name}</Text>
        <Text style={styles.cardSubtitle}>
          {laptop.host}:{laptop.port} · v{status?.serviceVersion ?? '?'}
        </Text>
      </View>

      <Pressable
        style={[styles.lockButton, (detail.isLocking || !client) && styles.lockButtonDisabled]}
        disabled={detail.isLocking || !client}
        onPress={() => void detail.lock()}
      >
        <Text style={styles.lockButtonText}>{detail.isLocking ? 'LOCKING…' : '🔒 LOCK LAPTOP'}</Text>
      </Pressable>

      {detail.error ? <Text style={styles.error}>{detail.error}</Text> : null}

      <View style={styles.grid}>
        <Stat label="Battery" value={status?.batteryPercent != null ? `${status.batteryPercent}%` : 'n/a'} />
        <Stat label="Proximity" value={proximity} accent={proximity === 'NEARBY' || proximity === 'AUTHENTICATED'} />
        <Stat label="Security" value={status?.security ?? '—'} />
        <Stat label="Auto-lock" value={autoLockLabel(detail.settings)} />
        <Stat label="Environment" value={status?.environment ?? '—'} />
        <Stat label="Auth" value={detail.authenticated ? 'Authenticated' : 'Not signed in'} />
      </View>

      <Pressable
        style={[styles.dangerButton, !client && styles.dangerButtonDisabled]}
        disabled={!client}
        onPress={() => {
          if (client) {
            void services.pairing.unpair(laptop.deviceId, client);
          }
        }}
      >
        <Text style={styles.dangerText}>Unpair</Text>
      </Pressable>
    </ScrollView>
  );
}

function Stat({ label, value, accent }: { label: string; value: string; accent?: boolean }) {
  return (
    <View style={styles.stat}>
      <Text style={styles.statLabel}>{label}</Text>
      <Text style={[styles.statValue, accent && styles.statAccent]}>{value}</Text>
    </View>
  );
}

function autoLockLabel(settings: { automaticLockEnabled: boolean; autoLockAwayDurationSeconds: number } | null): string {
  if (!settings) {
    return '—';
  }
  return settings.automaticLockEnabled ? `On · ${settings.autoLockAwayDurationSeconds}s` : 'Off';
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#0b1220' },
  content: { padding: 16, gap: 16 },
  center: { flex: 1, backgroundColor: '#0b1220', alignItems: 'center', justifyContent: 'center' },
  headerRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  dot: { width: 12, height: 12, borderRadius: 6 },
  dotConnected: { backgroundColor: '#30d158' },
  dotLocked: { backgroundColor: '#ff9f0a' },
  headerText: { color: '#e6f1ff', fontSize: 18, fontWeight: '700' },
  card: { backgroundColor: '#1a2536', borderRadius: 12, padding: 16 },
  cardTitle: { color: '#e6f1ff', fontSize: 20, fontWeight: '700' },
  cardSubtitle: { color: '#7a8aa0', fontSize: 13, marginTop: 4 },
  lockButton: {
    backgroundColor: '#ff453a',
    borderRadius: 12,
    paddingVertical: 16,
    alignItems: 'center',
  },
  lockButtonDisabled: { opacity: 0.5 },
  lockButtonText: { color: '#fff', fontSize: 17, fontWeight: '800' },
  error: { color: '#ff6961', textAlign: 'center' },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  stat: { backgroundColor: '#1a2536', borderRadius: 12, padding: 12, minWidth: '46%', flexGrow: 1 },
  statLabel: { color: '#7a8aa0', fontSize: 12 },
  statValue: { color: '#e6f1ff', fontSize: 16, fontWeight: '600', marginTop: 4 },
  statAccent: { color: '#30d158' },
  dangerButton: { backgroundColor: '#2a1a1c', borderRadius: 10, paddingVertical: 12, alignItems: 'center' },
  dangerButtonDisabled: { opacity: 0.4 },
  dangerText: { color: '#ff6961', fontWeight: '600' },
});