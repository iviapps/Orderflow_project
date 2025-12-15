const gatewayUrl =
  (import.meta.env.VITE_API_GATEWAY_URL as string | undefined) ??
  (import.meta.env.VITE_API_BASE_URL as string | undefined);

if (!gatewayUrl) {
  throw new Error(
    "Missing API base URL. Aspire should provide VITE_API_GATEWAY_URL."
  );
}

export const config = {
  apiBaseUrl: gatewayUrl,
  apiPrefix: "/api/v1", // fijamos v1 en frontend
};
