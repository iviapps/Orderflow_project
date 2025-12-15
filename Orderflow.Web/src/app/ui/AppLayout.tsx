import { Outlet, Link } from "react-router-dom";

export function AppLayout() {
  return (
    <div className="min-h-full">
      <header className="border-b">
        <div className="mx-auto max-w-6xl p-4 flex items-center gap-4">
          <Link to="/" className="font-semibold">
            OrderFlow
          </Link>

          <nav className="ml-auto flex items-center gap-3">
            <Link to="/" className="text-sm text-gray-600 hover:text-gray-900">
              Products
            </Link>
            <Link to="/login" className="text-sm text-gray-600 hover:text-gray-900">
              Login
            </Link>
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-6xl p-4">
        <Outlet />
      </main>
    </div>
  );
}
