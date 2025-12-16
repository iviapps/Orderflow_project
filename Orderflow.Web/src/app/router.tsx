import { createBrowserRouter } from "react-router-dom";
import { AppLayout } from "./ui/AppLayout";
import { LoginPage } from "../features/auth/pages/LoginPage";
import { RegisterPage } from "../features/auth/pages/RegisterPage";
import { ProductsPage } from "../features/catalog/pages/ProductsPage";
import { OrdersPage } from "../features/orders/pages/OrdersPage";
import { ProfilePage } from "../features/profile/pages/ProfilePage";
import { AdminUsersPage } from "../features/admin/pages/AdminUsersPage";

export const router = createBrowserRouter([
    {
        element: <AppLayout />,
        children: [
            { path: "/", element: <ProductsPage /> },
            { path: "/login", element: <LoginPage /> },
            { path: "/register", element: <RegisterPage /> },
            { path: "/orders", element: <OrdersPage /> },
            { path: "/profile", element: <ProfilePage /> },
            { path: "/admin/users", element: <AdminUsersPage /> },
        ],
    },
]);
