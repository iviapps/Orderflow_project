import { api } from "../../lib/api";

export async function getProducts() {
    const { data } = await api.get("/api/v1/products");
    return data;
}
