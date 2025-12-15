import { createBrowserRouter } from "react-router-dom";
import { AppLayout } from "./ui/AppLayout";
import { LoginPage } from "../features/auth/pages/LoginPage";
import { ProductsPage } from "../features/catalog/pages/ProductsPage";

export const router = createBrowserRouter([
    {
        element: <AppLayout />,
        children: [
            { path: "/", element: <ProductsPage /> },
            { path: "/login", element: <LoginPage /> },
        ],
    },
]);
