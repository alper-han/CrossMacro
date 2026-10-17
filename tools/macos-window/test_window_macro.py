import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import window_macro as macro


class WindowMacroTests(unittest.TestCase):
    def workflow(self):
        return json.loads((Path(__file__).parent/'wos-profile.json').read_text())

    def test_ocr_accepts_whitespace_and_width_variants(self):
        macro.require_text('ＩＤ： １２３\n领主 档案',['ID:123','领主档案'])

    def test_ocr_rejects_missing_identity(self):
        with self.assertRaises(RuntimeError):
            macro.require_text('ID:123',['124'])

    def test_rejects_click_without_visual_completion(self):
        workflow = self.workflow()
        del workflow['steps'][1]['after']
        with self.assertRaises(ValueError):
            macro.validate_workflow(workflow)

    def test_rejects_out_of_window_point(self):
        workflow = self.workflow()
        workflow['steps'][1]['point'] = [788,65]
        with self.assertRaises(ValueError):
            macro.validate_workflow(workflow)

    def test_export_keeps_steps_editable_and_uses_no_native_clicks(self):
        with tempfile.TemporaryDirectory(prefix='macro test ') as directory:
            output = Path(directory)/'sample.macro'
            macro.export_macro(Path(directory)/'config.json',Path(__file__).parent/'wos-profile.json',output)
            content = output.read_text()
            self.assertEqual(4,len([line for line in content.splitlines() if line.startswith('shell ')]))
            self.assertIn('04-return-city',content)
            self.assertNotIn('\nclick ',content)

    def test_failed_ocr_sends_no_click_and_logs_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            config = {'log_dir':directory}
            with patch.object(macro,'CuaMcp') as client_class:
                client = client_class.return_value
                client.observe.return_value = 'Unexpected dialog'
                with self.assertRaises(RuntimeError):
                    macro.execute(config,self.workflow(),'02-open-profile')
                client.js.assert_not_called()
                client.close.assert_called_once()
            reports = list(Path(directory).glob('*/result.json'))
            self.assertEqual('failed',json.loads(reports[0].read_text())['status'])

    def test_busy_coordinator_prevents_backend_start(self):
        with tempfile.TemporaryDirectory() as directory:
            config = {'log_dir':directory,'guard_command':['coordination','status']}
            with patch.object(macro.subprocess,'check_output',return_value='{"active":{"role":"other"}}'), patch.object(macro,'CuaMcp') as client:
                with self.assertRaises(RuntimeError):
                    macro.execute(config,self.workflow(),'01-check-city')
                client.assert_not_called()

    def test_worker_reuses_backend_without_starting_or_closing_it_per_step(self):
        from unittest.mock import MagicMock
        with tempfile.TemporaryDirectory() as directory:
            backend = MagicMock()
            backend.observe.return_value = '英雄 背包 野外'
            config = {'log_dir':directory}
            macro.execute(config,self.workflow(),'01-check-city',backend=backend)
            macro.execute(config,self.workflow(),'01-check-city',backend=backend)
            self.assertEqual(2,backend.observe.call_count)
            backend.start.assert_not_called()
            backend.close.assert_not_called()


if __name__ == '__main__':
    unittest.main()
