import { useCallback, useEffect, useState } from 'react';
import { loadLaptops } from '../storage/laptopStore';
import { useServices } from '../services/servicesContext';
import type { DiscoveredLaptop, PairedLaptop } from '../types/protocol';
import { FaceIdCancelledError } from '../api/errors';

export interface LaptopsState {
  paired: PairedLaptop[];
  discovered: DiscoveredLaptop[];
  refreshing: boolean;
  refreshDiscovery: () => void;
  unpair: (deviceId: string) => Promise<void>;
}

export function useLaptops(): LaptopsState {
  const services = useServices();
  const [paired, setPaired] = useState<PairedLaptop[]>([]);
  const [discovered, setDiscovered] = useState<DiscoveredLaptop[]>([]);
  const [refreshing, setRefreshing] = useState(false);

  useEffect(() => {
    let active = true;
    void loadLaptops().then((laptops) => {
      if (active) {
        setPaired(laptops);
      }
    });
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    const seen = new Map<string, DiscoveredLaptop>();
    const unsubscribe = services.discovery.start((laptop) => {
      seen.set(laptop.deviceId, laptop);
      setDiscovered(Array.from(seen.values()));
    });
    setRefreshing(true);
    return () => {
      unsubscribe();
      services.discovery.stop();
    };
  }, [services]);

  const refreshDiscovery = useCallback(() => {
    setRefreshing(true);
    services.discovery.stop();
    services.discovery.start((laptop) => {
      setDiscovered((current) => {
        const seen = new Map(current.map((l) => [l.deviceId, l]));
        seen.set(laptop.deviceId, laptop);
        return Array.from(seen.values());
      });
    });
  }, [services]);

  const unpair = useCallback(
    async (deviceId: string) => {
      const laptop = paired.find((l) => l.deviceId === deviceId);
      if (!laptop) {
        return;
      }
      try {
        const client = await services.createClient(laptop);
        await services.pairing.unpair(deviceId, client);
        setPaired(await loadLaptops());
      } catch (error) {
        if (error instanceof FaceIdCancelledError) {
          return;
        }
        services.logStore.add('UNPAIR_FAILED', error instanceof Error ? error.message : 'Unpair failed');
        throw error;
      }
    },
    [paired, services],
  );

  return { paired, discovered, refreshing, refreshDiscovery, unpair };
}