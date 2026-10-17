import { useEffect } from 'react';
import { useShell } from '@kododo-dev/way-shell';

/** Replaces the current address with the Way's root, e.g. for an unknown section. */
const ToWayRoot = () => {
  const { navigate, wayId } = useShell();
  useEffect(() => navigate(wayId, '', { replace: true }), [navigate, wayId]);
  return null;
};

export default ToWayRoot;
