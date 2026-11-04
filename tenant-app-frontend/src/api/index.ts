import { backend } from './backend';
import { mock } from './mock';
import { USE_MOCK } from './http';
// Demo mode is explicit and never used as a fallback after a server error.
export const api = USE_MOCK ? { ...backend, ...mock, logout: async () => {}, inquire: async (_id: string, _name: string, _phone: string, _email: string) => { throw new Error('Заявки на аренду доступны при подключении к серверу.'); } } : backend;
