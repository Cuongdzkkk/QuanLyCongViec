import axios from 'axios';
import { clearAuthSession, getStoredAccessToken } from '@/utils/authSession'
import { translateDemoPayload } from '@/utils/demoContentLocale'

const baseURL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5136/api';

const axiosClient = axios.create({
    baseURL: baseURL,
    headers: {
        'Content-Type': 'application/json'
    },
    withCredentials: true // Rất quan trọng để đính kèm HttpOnly cookies (RefreshToken)
});

let isRefreshing = false;
let failedQueue = [];

const processQueue = (error, token = null) => {
    failedQueue.forEach(prom => {
        if (error) {
            prom.reject(error);
        } else {
            prom.resolve(token);
        }
    });

    failedQueue = [];
};

axiosClient.interceptors.request.use(
    (config) => {
        const token = getStoredAccessToken();
        if (token) {
            config.headers['Authorization'] = `Bearer ${token}`;
        }
        const locale = localStorage.getItem('admin_locale') || 'vi';
        config.headers['Accept-Language'] = locale;
        return config;
    },
    (error) => {
        return Promise.reject(error);
    }
);

axiosClient.interceptors.response.use(
    (response) => {
        const data = response.data;
        const isBinaryPayload = (typeof Blob !== 'undefined' && data instanceof Blob)
            || (typeof ArrayBuffer !== 'undefined' && data instanceof ArrayBuffer)
            || (typeof ArrayBuffer !== 'undefined' && ArrayBuffer.isView(data));
        if (isBinaryPayload) return response;
        const locale = localStorage.getItem('admin_locale') || 'vi';
        response.data = translateDemoPayload(data, locale);
        return response;
    },
    async (error) => {
        const originalRequest = error.config;
        if (!originalRequest) {
            return Promise.reject(error);
        }
        const requestUrl = String(originalRequest.url || '');

        const isAuthRequest = requestUrl.includes('/auth/login') ||
                              requestUrl.includes('/auth/register') ||
                              requestUrl.includes('/auth/send-otp') ||
                              requestUrl.includes('/auth/verify-otp') ||
                              requestUrl.includes('/auth/reset-password') ||
                              requestUrl.includes('/auth/refresh-token') ||
                              requestUrl.includes('/auth/google-login') ||
                              requestUrl.includes('/auth/github-login') ||
                              requestUrl.includes('/auth/invite-info') ||
                              requestUrl.includes('/auth/accept-invite-token');

        if (error.response?.status === 401 && !originalRequest._retry && !isAuthRequest) {
            if (isRefreshing) {
                return new Promise(function (resolve, reject) {
                    failedQueue.push({ resolve, reject });
                }).then(token => {
                    originalRequest.headers = originalRequest.headers || {};
                    originalRequest.headers['Authorization'] = 'Bearer ' + token;
                    return axiosClient(originalRequest);
                }).catch(err => {
                    return Promise.reject(err);
                });
            }

            originalRequest._retry = true;
            isRefreshing = true;

            try {
                // Call refresh-token API. Cookie is automatically sent due to withCredentials
                // Backend requires the expired access token in the header to identify the user
                const accessToken = getStoredAccessToken();
                const authHeaders = accessToken ? { 'Authorization': `Bearer ${accessToken}` } : {};
                const { data } = await axios.post(`${baseURL}/auth/refresh-token`, {}, {
                    headers: authHeaders,
                    withCredentials: true
                });

                const newAccessToken = data?.data?.accessToken ?? data?.accessToken;
                if (!newAccessToken) {
                    throw new Error('Refresh token response did not include an access token.');
                }
                sessionStorage.setItem('accessToken', newAccessToken);
                localStorage.removeItem('accessToken');

                axiosClient.defaults.headers.common['Authorization'] = `Bearer ${newAccessToken}`;
                originalRequest.headers = originalRequest.headers || {};
                originalRequest.headers['Authorization'] = `Bearer ${newAccessToken}`;

                processQueue(null, newAccessToken);
                return axiosClient(originalRequest);
            } catch (err) {
                processQueue(err, null);
                const refreshStatus = err?.response?.status
                const shouldForceLogout = refreshStatus === 401 || refreshStatus === 403

                if (shouldForceLogout) {
                    clearAuthSession();
                    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
                    const redirect = currentPath && !currentPath.startsWith('/login')
                        ? `?redirect=${encodeURIComponent(currentPath)}`
                        : '';
                    window.location.href = `/login${redirect}`;
                }

                return Promise.reject(err);
            } finally {
                isRefreshing = false;
            }
        }

        if (error.response && error.response.status === 409) {
            console.warn('Conflict detected:', error.response.data);
            // Optionally, we could emit a global event or show a notification
            // but for now, we'll let the component handle the catch block
        }

        return Promise.reject(error);
    }
);

export default axiosClient;
