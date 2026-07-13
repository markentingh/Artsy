import axios from 'axios';

const instance = axios.create({
  headers: {
    'Content-Type': 'application/json'
  }
});

export function UseAxios(session) {
  const { token } = session || {};

  if (token) {
    instance.defaults.headers.common['Authorization'] = `Bearer ${token}`;
  } else {
    delete instance.defaults.headers.common['Authorization'];
  }

  return instance;
}
