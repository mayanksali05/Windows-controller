import { Stack } from 'expo-router';
import { ServicesProvider } from '../src/services/servicesContext';

export default function RootLayout() {
  return (
    <ServicesProvider>
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: '#0b1220' },
          headerTintColor: '#e6f1ff',
          contentStyle: { backgroundColor: '#0b1220' },
        }}
      >
        <Stack.Screen name="index" options={{ title: 'WinLock' }} />
        <Stack.Screen name="laptop/[id]" options={{ title: 'Laptop' }} />
        <Stack.Screen name="pair" options={{ title: 'Pair laptop' }} />
        <Stack.Screen name="settings" options={{ title: 'Settings' }} />
        <Stack.Screen name="logs" options={{ title: 'Security log' }} />
      </Stack>
    </ServicesProvider>
  );
}