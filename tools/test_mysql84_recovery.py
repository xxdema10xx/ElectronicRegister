"""Negative controls for selecting a port from an attested Docker binding."""
import unittest
from mysql84_recovery import published_port


class PublishedPortTests(unittest.TestCase):
    def binding(self):
        return dict(ports={'3306/tcp':[dict(HostIp='', HostPort='')]},
                    publishedPorts={'3306/tcp':[dict(HostIp='0.0.0.0', HostPort='32769'),
                                                dict(HostIp='::', HostPort='32769')]})

    def test_dynamic_and_fixed_ports(self):
        binding = self.binding()
        self.assertEqual(published_port(binding), 32769)
        binding['ports']['3306/tcp'][0]['HostPort'] = '32769'
        self.assertEqual(published_port(binding), 32769)

    def test_refuses_arbitrary_missing_ambiguous_or_protected_targets(self):
        for address, port in [('192.0.2.1','32769'), ('0.0.0.0','3307'),
                              ('0.0.0.0','0'), ('0.0.0.0','65536'), ('::','32769')]:
            with self.subTest(address=address, port=port):
                binding = self.binding()
                binding['publishedPorts']['3306/tcp'] = [dict(HostIp=address,HostPort=port)]
                with self.assertRaises(RuntimeError): published_port(binding)
        for field in ('ports','publishedPorts'):
            binding = self.binding()
            binding[field] = {}
            with self.assertRaises(RuntimeError): published_port(binding)
        binding = self.binding()
        binding['publishedPorts']['3306/tcp'][1]['HostPort'] = '32770'
        with self.assertRaises(RuntimeError): published_port(binding)
        binding = self.binding()
        binding['ports']['3306/tcp'][0]['HostPort'] = '32770'
        with self.assertRaises(RuntimeError): published_port(binding)


if __name__ == '__main__': unittest.main()
