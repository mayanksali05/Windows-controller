import { useLocalSearchParams } from 'expo-router';
import React, { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
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

  const detail = useLaptopDetail(client, laptop?.deviceId ?? null);
  const [editingPassword, setEditingPassword] = useState(false);
  const [passwordDraft, setPasswordDraft] = useState('');
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

      <View style={styles.actionRow}>
        <Pressable
          style={[styles.actionButton, styles.lockButton, (detail.isLocking || !client) && styles.buttonDisabled]}
          disabled={detail.isLocking || detail.isUnlocking || !client}
          onPress={() => void detail.lock()}
        >
          <Text style={styles.actionButtonText}>{detail.isLocking ? 'LOCKING…' : '🔒 LOCK'}</Text>
        </Pressable>
        <Pressable
          style={[
            styles.actionButton,
            styles.unlockButton,
            (detail.isUnlocking || !client || !detail.hasPassword) && styles.buttonDisabled,
          ]}
          disabled={detail.isUnlocking || detail.isLocking || !client || !detail.hasPassword}
          onPress={() => void detail.unlock()}
        >
          <Text style={styles.actionButtonText}>{detail.isUnlocking ? 'UNLOCKING…' : '🔓 UNLOCK'}</Text>
        </Pressable>
      </View>

      {detail.notice ? <Text style={styles.notice}>{detail.notice}</Text> : null}
      {!detail.hasPassword ? (
        <Text style={styles.hint}>Save your Windows password below to enable Unlock.</Text>
      ) : status?.unlockAvailable === false ? (
        <Text style={styles.hint}>
          Unlock is not set up on the laptop yet: run scripts\install-credential-provider.ps1 there (as administrator).
        </Text>
      ) : null}
      {detail.error ? <Text style={styles.error}>{detail.error}</Text> : null}

      <View style={styles.grid}>
        <Stat label="Battery" value={status?.batteryPercent != null ? `${status.batteryPercent}%` : 'n/a'} />
        <Stat label="Proximity" value={proximity} accent={proximity === 'NEARBY' || proximity === 'AUTHENTICATED'} />
        <Stat label="Security" value={status?.security ?? '—'} />
        <Stat label="Auto-lock" value={autoLockLabel(detail.settings)} />
        <Stat label="Environment" value={status?.environment ?? '—'} />
        <Stat label="Auth" value={detail.authenticated ? 'Authenticated' : 'Not signed in'} />
      </View>

      <View style={styles.card}>
        <Text style={styles.sectionTitle}>Unlock password</Text>
        <Text style={styles.cardSubtitle}>
          Your Windows account password (not your PIN). For a Microsoft account, use the Microsoft
          account password. It stays in this iPhone's Keychain and is only sent to the laptop after
          Face ID.
        </Text>
        {editingPassword ? (
          <>
            <TextInput
              style={styles.input}
              value={passwordDraft}
              onChangeText={setPasswordDraft}
              placeholder="Windows password"
              placeholderTextColor="#7a8aa0"
              secureTextEntry
              autoCapitalize="none"
              autoCorrect={false}
              textContentType="password"
            />
            <View style={styles.inlineRow}>
              <Pressable
                style={[styles.smallButton, !passwordDraft && styles.buttonDisabled]}
                disabled={!passwordDraft}
                onPress={() => {
                  void detail.savePassword(passwordDraft).then(() => {
                    setPasswordDraft('');
                    setEditingPassword(false);
                  });
                }}
              >
                <Text style={styles.smallButtonText}>Save</Text>
              </Pressable>
              <Pressable
                style={styles.smallButton}
                onPress={() => {
                  setPasswordDraft('');
                  setEditingPassword(false);
                }}
              >
                <Text style={styles.smallButtonText}>Cancel</Text>
              </Pressable>
            </View>
          </>
        ) : (
          <View style={styles.inlineRow}>
            <Text style={styles.statValue}>{detail.hasPassword ? 'Saved' : 'Not set'}</Text>
            <Pressable style={styles.smallButton} onPress={() => setEditingPassword(true)}>
              <Text style={styles.smallButtonText}>{detail.hasPassword ? 'Change' : 'Set password'}</Text>
            </Pressable>
            {detail.hasPassword ? (
              <Pressable style={styles.smallButton} onPress={() => void detail.removePassword()}>
                <Text style={styles.smallButtonText}>Remove</Text>
              </Pressable>
            ) : null}
          </View>
        )}
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
  actionRow: { flexDirection: 'row', gap: 12 },
  actionButton: { flex: 1, borderRadius: 12, paddingVertical: 16, alignItems: 'center' },
  lockButton: { backgroundColor: '#ff453a' },
  unlockButton: { backgroundColor: '#30a14e' },
  buttonDisabled: { opacity: 0.5 },
  actionButtonText: { color: '#fff', fontSize: 17, fontWeight: '800' },
  notice: { color: '#30d158', textAlign: 'center' },
  hint: { color: '#7a8aa0', textAlign: 'center', fontSize: 13 },
  sectionTitle: { color: '#5ac8fa', fontSize: 13, fontWeight: '700', textTransform: 'uppercase' },
  input: {
    backgroundColor: '#0b1220',
    color: '#e6f1ff',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    marginTop: 12,
  },
  inlineRow: { flexDirection: 'row', alignItems: 'center', gap: 10, marginTop: 12 },
  smallButton: { backgroundColor: '#0b1220', borderRadius: 8, paddingHorizontal: 12, paddingVertical: 8 },
  smallButtonText: { color: '#5ac8fa', fontWeight: '600' },
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