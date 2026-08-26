import React from 'react';
import { FlatList, StyleSheet, Text, View } from 'react-native';
import { useLogs } from '../src/hooks/useLogs';

export default function LogsScreen() {
  const entries = useLogs();

  return (
    <FlatList
      style={styles.screen}
      contentContainerStyle={styles.content}
      data={entries}
      keyExtractor={(item) => item.id}
      ListEmptyComponent={<Text style={styles.hint}>No security events yet.</Text>}
      renderItem={({ item }) => (
        <View style={styles.row}>
          <Text style={styles.message}>{item.message}</Text>
          <Text style={styles.meta}>
            {new Date(item.date).toLocaleTimeString()} · {item.kind}
          </Text>
        </View>
      )}
    />
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#0b1220' },
  content: { padding: 16, gap: 10 },
  hint: { color: '#7a8aa0', textAlign: 'center', marginTop: 32 },
  row: { backgroundColor: '#1a2536', borderRadius: 10, padding: 12 },
  message: { color: '#e6f1ff', fontSize: 14 },
  meta: { color: '#7a8aa0', fontSize: 12, marginTop: 4 },
});