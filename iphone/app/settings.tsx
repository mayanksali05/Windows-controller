import { Link } from 'expo-router';
import React, { useEffect, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useServices } from '../src/services/servicesContext';
import { loadLaptops } from '../src/storage/laptopStore';
import type { PairedLaptop } from '../src/types/protocol';

export default function SettingsScreen() {
  const services = useServices();
  const [laptops, setLaptops] = useState<PairedLaptop[]>([]);
  const [bluetooth, setBluetooth] = useState<string>('UNKNOWN');

  useEffect(() => {
    void loadLaptops().then(setLaptops);
    setBluetooth(services.bluetooth.getAdapterState());
  }, [services]);

  return (
    <ScrollView style={styles.screen} contentContainerStyle={styles.content}>
      <Section title="Device">
        <Row label="Device ID" value={services.identity.deviceId} />
        <Row label="Identity" value="Ed25519 · iOS Keychain" />
      </Section>

      <Section title="Paired laptops">
        {laptops.length === 0 ? (
          <Text style={styles.hint}>None paired.</Text>
        ) : (
          laptops.map((l) => (
            <Link key={l.deviceId} href={`/laptop/${l.deviceId}`}>
              <Row label={l.name} value={`${l.host}:${l.port}`} />
            </Link>
          ))
        )}
      </Section>

      <Section title="Bluetooth">
        <Row label="Adapter" value={bluetooth} />
        <Row label="Role" value="Advertiser (laptop scans)" />
        <Text style={styles.note}>
          BLE is a proximity signal only and never authenticates. Proximity is
          configured and measured on the Windows laptop.
        </Text>
      </Section>

      <Section title="Security">
        <Row label="Transport" value="TLS, certificate pinned" />
        <Row label="Authentication" value="Face ID + Ed25519 challenge-response" />
        <Row label="Unlock" value="Extension point (not implemented)" />
      </Section>

      <Section title="Network">
        <Row label="Discovery" value="Bonjour · _mywinlock._tcp" />
      </Section>

      <Section title="About">
        <Row label="Client" value="WinLock · Expo / React Native" />
        <Link href="/logs">
          <Row label="Security log" value="View" />
        </Link>
      </Section>
    </ScrollView>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>{title}</Text>
      <View style={styles.sectionBody}>{children}</View>
    </View>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#0b1220' },
  content: { padding: 16, gap: 16 },
  section: { gap: 8 },
  sectionTitle: { color: '#5ac8fa', fontSize: 13, fontWeight: '700', textTransform: 'uppercase' },
  sectionBody: { backgroundColor: '#1a2536', borderRadius: 12, padding: 12, gap: 10 },
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: 8 },
  rowLabel: { color: '#7a8aa0', fontSize: 14 },
  rowValue: { color: '#e6f1ff', fontSize: 14, fontWeight: '600', flexShrink: 1, textAlign: 'right' },
  hint: { color: '#7a8aa0', fontSize: 13 },
  note: { color: '#7a8aa0', fontSize: 12, marginTop: 4 },
});