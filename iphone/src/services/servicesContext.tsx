import React, { createContext, useContext, useEffect, useState } from 'react';
import { Text, View, ActivityIndicator } from 'react-native';
import { createAppServices, type AppServices } from './appServices';

const ServicesContext = createContext<AppServices | null>(null);

/**
 * Creates the app service container once and provides it to the tree. The app
 * cannot render privileged UI until the identity is loaded.
 */
export function ServicesProvider({ children }: { children: React.ReactNode }) {
  const [services, setServices] = useState<AppServices | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    createAppServices()
      .then((created) => {
        if (active) {
          setServices(created);
        }
      })
      .catch((err: unknown) => {
        if (active) {
          setError(err instanceof Error ? err.message : 'Failed to initialize');
        }
      });
    return () => {
      active = false;
    };
  }, []);

  if (error) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', padding: 24 }}>
        <Text>Failed to initialize: {error}</Text>
      </View>
    );
  }
  if (!services) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center' }}>
        <ActivityIndicator />
      </View>
    );
  }
  return <ServicesContext.Provider value={services}>{children}</ServicesContext.Provider>;
}

export function useServices(): AppServices {
  const services = useContext(ServicesContext);
  if (!services) {
    throw new Error('useServices must be used within ServicesProvider');
  }
  return services;
}